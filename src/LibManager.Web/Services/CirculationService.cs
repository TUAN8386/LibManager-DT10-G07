// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W4-01/W4-03 – CirculationService mượn – trả, FineCalculator (tuần 4, nhánh feature/tuan-luu-thong)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using LibManager.Web.Data;
using LibManager.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibManager.Web.Services;

public interface ICirculationService
{
    /// <summary>Thông tin độc giả theo mã thẻ kèm kết luận có được mượn hay không (BR-02, BR-03).</summary>
    Task<ServiceResult<ReaderEligibilityDto>> GetReaderAsync(string cardNumber);
    /// <summary>Kiểm tra nhanh một bản sao vừa quét có thêm được vào phiếu mượn không (UC-M1 bước 4).</summary>
    Task<ServiceResult<CopyCheckDto>> CheckCopyAsync(string cardNumber, string barcode, IReadOnlyCollection<string> pendingBarcodes);
    /// <summary>Lập phiếu mượn trong một transaction (UC-M1 bước 6).</summary>
    Task<ServiceResult<CheckoutResultDto>> CheckoutAsync(CheckoutRequest request, string? librarianId);
    /// <summary>Nhận trả một bản sao, tính phí quá hạn, chuyển cho người đặt giữ (UC-M2).</summary>
    Task<ServiceResult<CheckinResultDto>> CheckinAsync(CheckinRequest request, string? librarianId);
    /// <summary>Gia hạn (BR-05). onlyForReaderId != null khi độc giả tự gia hạn.</summary>
    Task<ServiceResult<DateTime>> RenewAsync(int loanItemId, int? onlyForReaderId);
    Task<ServiceResult> MarkLostAsync(int loanItemId);
    Task<ServiceResult> PayFineAsync(int fineId);
}

public class CirculationService : ICirculationService
{
    private readonly LibraryDbContext _db;
    private readonly LibraryOptions _opt;
    private readonly TimeProvider _time;
    private readonly IReservationService _reservations;
    private readonly IAppEmailSender _email;
    private readonly ILogger<CirculationService> _log;

    public CirculationService(LibraryDbContext db, IOptions<LibraryOptions> opt, TimeProvider time,
        IReservationService reservations, IAppEmailSender email, ILogger<CirculationService> log)
    {
        _db = db;
        _opt = opt.Value;
        _time = time;
        _reservations = reservations;
        _email = email;
        _log = log;
    }

    private DateTime Now => _time.GetLocalNow().DateTime;

    // ------------------------------------------------------------------ Độc giả
    public async Task<ServiceResult<ReaderEligibilityDto>> GetReaderAsync(string cardNumber)
    {
        var info = BarcodeRules.Classify(cardNumber);
        if (info.Type != CodeType.ReaderCard)
            return ServiceResult<ReaderEligibilityDto>.Fail("INVALID_CARD", "Mã thẻ độc giả không hợp lệ (định dạng DG + 8 chữ số).");

        var reader = await _db.Readers.AsNoTracking().Include(r => r.ReaderType)
            .FirstOrDefaultAsync(r => r.CardNumber == info.Normalized);
        if (reader is null)
            return ServiceResult<ReaderEligibilityDto>.Fail("CARD_NOT_FOUND", $"Không tìm thấy độc giả có mã thẻ {info.Normalized}.");

        return ServiceResult<ReaderEligibilityDto>.Ok(await BuildEligibilityAsync(reader));
    }

    private async Task<ReaderEligibilityDto> BuildEligibilityAsync(Reader reader)
    {
        var now = Now;
        var type = reader.ReaderType!;

        var loans = await _db.LoanItems.AsNoTracking()
            .Where(li => li.Loan!.ReaderId == reader.Id && li.Status == LoanItemStatus.Borrowing)
            .OrderBy(li => li.DueDate)
            .Select(li => new { li.Id, li.Copy!.Barcode, li.Copy.Book!.Title, li.Loan!.LoanDate, li.DueDate, li.RenewCount })
            .ToListAsync();

        var unpaid = await _db.Fines.Where(f => f.ReaderId == reader.Id && !f.IsPaid).SumAsync(f => f.Amount);

        var holds = await _db.Reservations.AsNoTracking()
            .Where(r => r.ReaderId == reader.Id && r.Status == ReservationStatus.Ready)
            .Select(r => new ReadyHoldDto(r.Id, r.Book!.Title, r.Copy != null ? r.Copy.Barcode : null, r.ExpiresAt))
            .ToListAsync();

        int overdue = loans.Count(l => l.DueDate < now);
        var reasons = new List<string>();
        if (reader.IsLocked) reasons.Add("Thẻ đang bị khóa.");
        if (reader.ExpiredAt.Date < now.Date) reasons.Add($"Thẻ đã hết hạn ngày {reader.ExpiredAt:dd/MM/yyyy}.");
        if (overdue > 0) reasons.Add($"Đang có {overdue} sách quá hạn chưa trả.");
        if (unpaid > _opt.MaxUnpaidFine) reasons.Add($"Phí chưa nộp {unpaid.FormatVnd()} vượt ngưỡng {_opt.MaxUnpaidFine.FormatVnd()}.");
        if (loans.Count >= type.MaxBooks) reasons.Add($"Đã mượn đủ hạn mức {type.MaxBooks} cuốn.");

        return new ReaderEligibilityDto
        {
            Id = reader.Id,
            CardNumber = reader.CardNumber,
            FullName = reader.FullName,
            ReaderType = type.Name,
            ExpiredAt = reader.ExpiredAt,
            IsLocked = reader.IsLocked,
            CurrentLoans = loans.Count,
            MaxBooks = type.MaxBooks,
            LoanDays = type.LoanDays,
            OverdueCount = overdue,
            UnpaidFines = unpaid,
            CanBorrow = reasons.Count == 0,
            Reasons = reasons,
            ReadyHolds = holds,
            Loans = loans.Select(l => new ReaderLoanDto(l.Id, l.Barcode, l.Title, l.LoanDate, l.DueDate,
                FineCalculator.OverdueDays(l.DueDate, now), l.RenewCount)).ToList(),
        };
    }

    // ------------------------------------------------------------------ Kiểm tra từng lượt quét
    public async Task<ServiceResult<CopyCheckDto>> CheckCopyAsync(string cardNumber, string barcode, IReadOnlyCollection<string> pendingBarcodes)
    {
        var readerRes = await GetReaderAsync(cardNumber);
        if (!readerRes.Success) return ServiceResult<CopyCheckDto>.Fail(readerRes.ErrorCode!, readerRes.Message);
        var reader = readerRes.Data!;
        if (!reader.CanBorrow)
            return ServiceResult<CopyCheckDto>.Fail("NOT_ELIGIBLE", "Độc giả không được mượn: " + string.Join(" ", reader.Reasons));

        var info = BarcodeRules.Classify(barcode);
        var formatError = ValidateCopyCode(info);
        if (formatError is not null) return ServiceResult<CopyCheckDto>.Fail(formatError.Value.code, formatError.Value.msg);

        var pending = pendingBarcodes.Select(BarcodeRules.Normalize).ToHashSet();
        if (pending.Contains(info.Normalized))
            return ServiceResult<CopyCheckDto>.Fail("DUPLICATE", "Bản sao này đã có trong danh sách mượn.");

        var copy = await _db.BookCopies.AsNoTracking()
            .Where(c => c.Barcode == info.Normalized)
            .Select(c => new
            {
                c.Id, c.Barcode, c.Status, c.ShelfLocation, c.Book!.Title, c.Book.IsReferenceOnly,
                Authors = c.Book.BookAuthors.OrderBy(a => a.Order).Select(a => a.Author!.Name).ToList(),
            })
            .FirstOrDefaultAsync();
        if (copy is null) return ServiceResult<CopyCheckDto>.Fail("COPY_NOT_FOUND", $"Không tìm thấy bản sao {info.Normalized}.");

        var statusError = await ValidateCopyStatusAsync(copy.Id, copy.Status, copy.IsReferenceOnly, reader.Id);
        if (statusError is not null) return ServiceResult<CopyCheckDto>.Fail(statusError.Value.code, statusError.Value.msg);

        if (reader.CurrentLoans + pending.Count + 1 > reader.MaxBooks)
            return ServiceResult<CopyCheckDto>.Fail("LIMIT_EXCEEDED",
                $"Vượt hạn mức: đang mượn {reader.CurrentLoans}, chờ lập phiếu {pending.Count}, tối đa {reader.MaxBooks} cuốn.");

        var due = FineCalculator.DueDate(Now, reader.LoanDays);
        return ServiceResult<CopyCheckDto>.Ok(new CopyCheckDto(copy.Id, copy.Barcode, copy.Title,
            string.Join(", ", copy.Authors), copy.ShelfLocation, due));
    }

    private static (string code, string msg)? ValidateCopyCode(CodeInfo info)
    {
        if (info.Type == CodeType.ReaderCard) return ("NOT_COPY_CODE", "Đây là mã thẻ độc giả, không phải mã bản sao.");
        if (info.Type == CodeType.Isbn) return ("NOT_COPY_CODE", "Đây là mã ISBN trên bìa. Hãy quét nhãn mã bản sao (BS…) dán trên sách.");
        if (info.Type != CodeType.Copy) return ("INVALID_CODE", info.Error ?? "Mã không hợp lệ.");
        if (!info.IsValid) return ("BAD_CHECK_DIGIT", info.Error!);
        return null;
    }

    private async Task<(string code, string msg)?> ValidateCopyStatusAsync(int copyId, CopyStatus status, bool referenceOnly, int readerId)
    {
        if (referenceOnly) return ("REFERENCE_ONLY", "Sách chỉ đọc tại chỗ, không cho mượn về.");
        switch (status)
        {
            case CopyStatus.Available:
                return null;
            case CopyStatus.OnHold:
                bool mine = await _db.Reservations.AnyAsync(r => r.CopyId == copyId && r.ReaderId == readerId
                                                                 && r.Status == ReservationStatus.Ready);
                return mine ? null : ("ON_HOLD_OTHER", "Bản sao đang được giữ cho độc giả khác.");
            case CopyStatus.OnLoan:
                var due = await _db.LoanItems.Where(li => li.CopyId == copyId && li.Status == LoanItemStatus.Borrowing)
                    .Select(li => (DateTime?)li.DueDate).FirstOrDefaultAsync();
                return ("COPY_NOT_AVAILABLE", due.HasValue
                    ? $"Bản sao đang được mượn (hạn trả {due:dd/MM/yyyy}). Nếu sách đang ở quầy, hãy làm thủ tục trả trước."
                    : "Bản sao đang ở trạng thái Đang mượn.");
            default:
                return ("COPY_NOT_AVAILABLE", $"Bản sao đang ở trạng thái “{status.DisplayName()}”, không thể cho mượn.");
        }
    }

    // ------------------------------------------------------------------ Lập phiếu mượn
    public async Task<ServiceResult<CheckoutResultDto>> CheckoutAsync(CheckoutRequest request, string? librarianId)
    {
        var codes = (request.Barcodes ?? new()).Select(BarcodeRules.Normalize).Where(c => c.Length > 0).Distinct().ToList();
        if (codes.Count == 0) return ServiceResult<CheckoutResultDto>.Fail("EMPTY", "Chưa có bản sao nào trong phiếu mượn.");

        foreach (var c in codes)
        {
            var err = ValidateCopyCode(BarcodeRules.Classify(c));
            if (err is not null) return ServiceResult<CheckoutResultDto>.Fail(err.Value.code, $"{c}: {err.Value.msg}");
        }

        var card = BarcodeRules.Normalize(request.CardNumber);
        var now = Now;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var reader = await _db.Readers.Include(r => r.ReaderType).FirstOrDefaultAsync(r => r.CardNumber == card);
            if (reader is null) return ServiceResult<CheckoutResultDto>.Fail("CARD_NOT_FOUND", "Không tìm thấy độc giả.");

            // Kiểm tra lại toàn bộ điều kiện ở máy chủ, trong transaction (không tin dữ liệu từ trình duyệt).
            var elig = await BuildEligibilityAsync(reader);
            if (!elig.CanBorrow)
                return ServiceResult<CheckoutResultDto>.Fail("NOT_ELIGIBLE", "Độc giả không được mượn: " + string.Join(" ", elig.Reasons));
            if (elig.CurrentLoans + codes.Count > elig.MaxBooks)
                return ServiceResult<CheckoutResultDto>.Fail("LIMIT_EXCEEDED", $"Vượt hạn mức {elig.MaxBooks} cuốn.");

            var copies = await _db.BookCopies
                .Include(c => c.Book!).ThenInclude(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                .Where(c => codes.Contains(c.Barcode))
                .ToListAsync();
            var missing = codes.Except(copies.Select(c => c.Barcode)).ToList();
            if (missing.Count > 0)
                return ServiceResult<CheckoutResultDto>.Fail("COPY_NOT_FOUND", "Không tìm thấy bản sao: " + string.Join(", ", missing));

            foreach (var copy in copies)
            {
                var err = await ValidateCopyStatusAsync(copy.Id, copy.Status, copy.Book!.IsReferenceOnly, reader.Id);
                if (err is not null) return ServiceResult<CheckoutResultDto>.Fail(err.Value.code, $"{copy.Barcode}: {err.Value.msg}");
            }

            var due = FineCalculator.DueDate(now, reader.ReaderType!.LoanDays);
            var loan = new Loan
            {
                Code = "PM" + now.ToString("yyMMddHHmmss") + Random.Shared.Next(10, 99),
                ReaderId = reader.Id,
                LibrarianId = librarianId,
                LoanDate = now,
            };
            foreach (var copy in copies)
            {
                copy.Status = CopyStatus.OnLoan;           // RowVersion của bản sao được kiểm tra khi SaveChanges
                loan.Items.Add(new LoanItem { CopyId = copy.Id, DueDate = due });
            }
            _db.Loans.Add(loan);

            // Hoàn tất yêu cầu đặt giữ nếu độc giả nhận đúng sách giữ cho mình.
            var copyIds = copies.Select(c => c.Id).ToList();
            var holds = await _db.Reservations
                .Where(r => r.ReaderId == reader.Id && r.Status == ReservationStatus.Ready && r.CopyId != null && copyIds.Contains(r.CopyId.Value))
                .ToListAsync();
            foreach (var h in holds) h.Status = ReservationStatus.Fulfilled;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var items = codes.Select(code => copies.First(c => c.Barcode == code)).Select(c => new CopyCheckDto(
                c.Id, c.Barcode, c.Book!.Title,
                string.Join(", ", c.Book.BookAuthors.OrderBy(a => a.Order).Select(a => a.Author!.Name)),
                c.ShelfLocation, due)).ToList();

            if (!string.IsNullOrEmpty(reader.Email))
                _ = _email.SendAsync(reader.Email, reader.FullName, $"[Thư viện] Xác nhận mượn sách {loan.Code}",
                    EmailTemplates.CheckoutConfirm(reader.FullName, loan.Code, items.Select(i => (i.Title, i.DueDate))));

            _log.LogInformation("Lập phiếu {Code} cho {Card}: {Count} bản sao", loan.Code, reader.CardNumber, items.Count);
            return ServiceResult<CheckoutResultDto>.Ok(new CheckoutResultDto(loan.Id, loan.Code, reader.FullName, now, items),
                $"Đã lập phiếu {loan.Code} – {items.Count} cuốn, hạn trả {due:dd/MM/yyyy}.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return ServiceResult<CheckoutResultDto>.Fail("CONFLICT", "Một bản sao vừa được quầy khác cập nhật. Vui lòng quét lại.");
        }
    }

    // ------------------------------------------------------------------ Trả sách
    public async Task<ServiceResult<CheckinResultDto>> CheckinAsync(CheckinRequest request, string? librarianId)
    {
        var info = BarcodeRules.Classify(request.Barcode);
        var formatError = ValidateCopyCode(info);
        if (formatError is not null) return ServiceResult<CheckinResultDto>.Fail(formatError.Value.code, formatError.Value.msg);

        var now = Now;
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var copy = await _db.BookCopies.Include(c => c.Book).FirstOrDefaultAsync(c => c.Barcode == info.Normalized);
            if (copy is null) return ServiceResult<CheckinResultDto>.Fail("COPY_NOT_FOUND", $"Không tìm thấy bản sao {info.Normalized}.");

            var item = await _db.LoanItems
                .Include(li => li.Loan!).ThenInclude(l => l.Reader!).ThenInclude(r => r.ReaderType)
                .Where(li => li.CopyId == copy.Id && li.Status == LoanItemStatus.Borrowing)
                .OrderByDescending(li => li.Id)
                .FirstOrDefaultAsync();
            if (item is null)
                return ServiceResult<CheckinResultDto>.Fail("NOT_ON_LOAN",
                    $"Bản sao không ở trạng thái đang mượn (hiện tại: {copy.Status.DisplayName()}).");

            var reader = item.Loan!.Reader!;
            int overdueDays = FineCalculator.OverdueDays(item.DueDate, now);
            decimal fineAmount = FineCalculator.OverdueFine(overdueDays, reader.ReaderType!.FinePerDay, copy.Book!.Price);

            item.ReturnedAt = now;
            item.Status = LoanItemStatus.Returned;

            Fine? fine = null;
            if (fineAmount > 0)
            {
                fine = new Fine { ReaderId = reader.Id, LoanItemId = item.Id, Amount = fineAmount, Reason = FineReason.Overdue,
                                  CreatedAt = now, Note = $"Quá hạn {overdueDays} ngày" };
                _db.Fines.Add(fine);
            }

            Reservation? hold = null;
            if (request.Damaged)
            {
                copy.Status = CopyStatus.Damaged;
                copy.Condition = string.IsNullOrWhiteSpace(request.Note) ? "Hư hỏng khi trả" : request.Note.Trim();
                if (request.DamageFee > 0)
                    _db.Fines.Add(new Fine { ReaderId = reader.Id, LoanItemId = item.Id, Amount = request.DamageFee,
                                             Reason = FineReason.Damaged, CreatedAt = now, Note = copy.Condition });
            }
            else
            {
                hold = await _reservations.AssignCopyToNextAsync(copy, now); // OnHold hoặc Available
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            if (hold?.Reader?.Email is not null && hold.ExpiresAt.HasValue)
                _ = _email.SendAsync(hold.Reader.Email, hold.Reader.FullName, "[Thư viện] Sách đặt giữ đã sẵn sàng",
                    EmailTemplates.HoldReady(hold.Reader.FullName, copy.Book.Title, hold.ExpiresAt.Value));

            var msg = fineAmount > 0
                ? $"Đã nhận trả. Quá hạn {overdueDays} ngày, phí {fineAmount.FormatVnd()}."
                : "Đã nhận trả đúng hạn.";
            if (hold is not null) msg += $" Để riêng – giữ cho {hold.Reader!.FullName}.";

            return ServiceResult<CheckinResultDto>.Ok(new CheckinResultDto
            {
                Barcode = copy.Barcode,
                Title = copy.Book.Title,
                ReaderName = reader.FullName,
                CardNumber = reader.CardNumber,
                LoanDate = item.Loan.LoanDate,
                DueDate = item.DueDate,
                ReturnedAt = now,
                OverdueDays = overdueDays,
                FineAmount = fineAmount,
                FineId = fine?.Id,
                NewStatus = copy.Status.DisplayName(),
                Shelf = copy.ShelfLocation,
                HoldForReader = hold?.Reader?.FullName,
            }, msg);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return ServiceResult<CheckinResultDto>.Fail("CONFLICT", "Bản sao vừa được quầy khác cập nhật. Vui lòng quét lại.");
        }
    }

    // ------------------------------------------------------------------ Gia hạn, mất sách, thu phí
    public async Task<ServiceResult<DateTime>> RenewAsync(int loanItemId, int? onlyForReaderId)
    {
        var item = await _db.LoanItems
            .Include(li => li.Loan!).ThenInclude(l => l.Reader!).ThenInclude(r => r.ReaderType)
            .Include(li => li.Copy)
            .FirstOrDefaultAsync(li => li.Id == loanItemId);
        if (item is null) return ServiceResult<DateTime>.Fail("NOT_FOUND", "Không tìm thấy lượt mượn.");
        var reader = item.Loan!.Reader!;
        if (onlyForReaderId.HasValue && reader.Id != onlyForReaderId) return ServiceResult<DateTime>.Fail("FORBIDDEN", "Bạn không có quyền gia hạn lượt mượn này.");
        if (item.Status != LoanItemStatus.Borrowing) return ServiceResult<DateTime>.Fail("INVALID_STATE", "Lượt mượn đã kết thúc.");

        var now = Now;
        var type = reader.ReaderType!;
        if (reader.IsLocked || reader.ExpiredAt.Date < now.Date) return ServiceResult<DateTime>.Fail("CARD_INVALID", "Thẻ bị khóa hoặc đã hết hạn.");
        if (item.DueDate < now) return ServiceResult<DateTime>.Fail("OVERDUE", "Sách đã quá hạn, không thể gia hạn. Vui lòng trả sách tại quầy.");
        if (item.RenewCount >= type.MaxRenewals) return ServiceResult<DateTime>.Fail("MAX_RENEWALS", $"Đã gia hạn tối đa {type.MaxRenewals} lần.");

        bool queued = await _db.Reservations.AnyAsync(r => r.BookId == item.Copy!.BookId && r.Status == ReservationStatus.Waiting);
        if (queued) return ServiceResult<DateTime>.Fail("HAS_RESERVATION", "Đầu sách đang có người đặt giữ, không thể gia hạn.");

        item.DueDate = FineCalculator.RenewedDueDate(item.DueDate, type.LoanDays);
        item.RenewCount++;
        item.RemindedAt = null;
        await _db.SaveChangesAsync();
        return ServiceResult<DateTime>.Ok(item.DueDate, $"Gia hạn thành công, hạn mới {item.DueDate:dd/MM/yyyy}.");
    }

    public async Task<ServiceResult> MarkLostAsync(int loanItemId)
    {
        var item = await _db.LoanItems.Include(li => li.Loan).Include(li => li.Copy!).ThenInclude(c => c.Book)
            .FirstOrDefaultAsync(li => li.Id == loanItemId);
        if (item is null) return ServiceResult.Fail("NOT_FOUND", "Không tìm thấy lượt mượn.");
        if (item.Status != LoanItemStatus.Borrowing) return ServiceResult.Fail("INVALID_STATE", "Lượt mượn đã kết thúc.");

        var now = Now;
        item.Status = LoanItemStatus.Lost;
        item.ReturnedAt = now;
        item.Copy!.Status = CopyStatus.Lost;
        var amount = FineCalculator.LostCompensation(item.Copy.Book!.Price, _opt.LostProcessingFee);
        _db.Fines.Add(new Fine { ReaderId = item.Loan!.ReaderId, LoanItemId = item.Id, Amount = amount, Reason = FineReason.Lost,
                                 CreatedAt = now, Note = $"Mất sách {item.Copy.Barcode}" });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("CONFLICT", "Bản sao vừa được cập nhật ở nơi khác. Vui lòng thử lại.");
        }
        return ServiceResult.Ok($"Đã ghi nhận mất sách. Bồi thường {amount.FormatVnd()}.");
    }

    public async Task<ServiceResult> PayFineAsync(int fineId)
    {
        var fine = await _db.Fines.FirstOrDefaultAsync(f => f.Id == fineId);
        if (fine is null) return ServiceResult.Fail("NOT_FOUND", "Không tìm thấy khoản phí.");
        if (fine.IsPaid) return ServiceResult.Fail("ALREADY_PAID", "Khoản phí đã được thu.");
        fine.IsPaid = true;
        fine.PaidAt = Now;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok($"Đã thu {fine.Amount.FormatVnd()}.");
    }
}
