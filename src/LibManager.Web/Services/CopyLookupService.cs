// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W5-07 – CopyLookupService tra cứu bản sao, ghi ScanLog (tuần 5, nhánh feature/long-tra-cuu)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.Diagnostics;
using LibManager.Web.Data;
using LibManager.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibManager.Web.Services;

public interface ICopyLookupService
{
    /// <summary>
    /// Tra cứu tức thời theo mã quét (UC-M3): mã bản sao → chi tiết bản sao;
    /// ISBN → tất cả bản sao của đầu sách; chuỗi khác → gợi ý đầu sách theo nhan đề/tác giả.
    /// </summary>
    Task<LookupResultDto> LookupAsync(string code, CancellationToken ct = default);
    Task<List<BookSuggestDto>> SuggestAsync(string term, int take = 8, CancellationToken ct = default);
}

public class CopyLookupService : ICopyLookupService
{
    private readonly LibraryDbContext _db;
    private readonly TimeProvider _time;

    public CopyLookupService(LibraryDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<LookupResultDto> LookupAsync(string code, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var info = BarcodeRules.Classify(code);
        LookupResultDto result = info.Type switch
        {
            CodeType.Copy when info.IsValid => await LookupCopyAsync(info.Normalized, ct),
            CodeType.Copy => new LookupResultDto { Code = info.Normalized, CodeType = "copy", Message = info.Error },
            CodeType.Isbn => await LookupIsbnAsync(info.Normalized, ct),
            CodeType.ReaderCard => new LookupResultDto
            {
                Code = info.Normalized, CodeType = "card",
                Message = "Đây là mã thẻ độc giả. Dùng màn hình Quầy lưu thông để mượn sách.",
            },
            _ => await LookupTextAsync(code?.Trim() ?? "", ct),
        };
        result.ElapsedMs = (int)sw.ElapsedMilliseconds;
        return result;
    }

    private async Task<LookupResultDto> LookupCopyAsync(string barcode, CancellationToken ct)
    {
        var now = _time.GetLocalNow().DateTime;

        // Truy vấn 1: bản sao + đầu sách (dùng chỉ mục UNIQUE trên Barcode).
        var c = await _db.BookCopies.AsNoTracking()
            .Where(x => x.Barcode == barcode)
            .Select(x => new
            {
                x.Id, x.Barcode, x.Status, x.ShelfLocation, x.Condition, x.AcquiredAt, x.BookId,
                x.Book!.Title, x.Book.Isbn, x.Book.CoverUrl, x.Book.PublishYear, x.Book.IsReferenceOnly,
                Category = x.Book.Category!.Name,
                Publisher = x.Book.Publisher != null ? x.Book.Publisher.Name : null,
                Authors = x.Book.BookAuthors.OrderBy(a => a.Order).Select(a => a.Author!.Name).ToList(),
                Total = x.Book.Copies.Count(k => k.Status != CopyStatus.Withdrawn),
                Available = x.Book.Copies.Count(k => k.Status == CopyStatus.Available),
            })
            .FirstOrDefaultAsync(ct);

        if (c is null)
            return new LookupResultDto { Code = barcode, CodeType = "copy", Found = false, Message = $"Không tìm thấy bản sao {barcode}." };

        // Truy vấn 2: 5 lượt lưu thông gần nhất (lượt đầu tiên nếu đang mượn chính là lượt hiện tại).
        var history = await _db.LoanItems.AsNoTracking()
            .Where(li => li.CopyId == c.Id)
            .OrderByDescending(li => li.Id)
            .Take(5)
            .Select(li => new { li.Id, li.Loan!.Reader!.CardNumber, li.Loan.Reader.FullName, li.Loan.LoanDate, li.DueDate, li.ReturnedAt, li.Status, li.RenewCount })
            .ToListAsync(ct);

        CurrentLoanDto? current = null;
        var open = history.FirstOrDefault(h => h.Status == LoanItemStatus.Borrowing);
        if (open is not null)
            current = new CurrentLoanDto(open.Id, open.CardNumber, open.FullName, open.LoanDate, open.DueDate,
                FineCalculator.OverdueDays(open.DueDate, now), open.RenewCount);

        HoldInfoDto? hold = null;
        if (c.Status == CopyStatus.OnHold)
            hold = await _db.Reservations.AsNoTracking()
                .Where(r => r.CopyId == c.Id && r.Status == ReservationStatus.Ready)
                .Select(r => new HoldInfoDto(r.Reader!.CardNumber, r.Reader.FullName, r.ExpiresAt))
                .FirstOrDefaultAsync(ct);

        return new LookupResultDto
        {
            Code = barcode,
            CodeType = "copy",
            Found = true,
            Copy = new CopyInfoDto(c.Id, c.Barcode, c.Status.ToString(), c.Status.DisplayName(), c.ShelfLocation, c.Condition, c.AcquiredAt),
            Book = new BookInfoDto(c.BookId, c.Title, string.Join(", ", c.Authors), c.Isbn, c.CoverUrl, c.Category, c.Publisher,
                c.PublishYear, c.IsReferenceOnly, c.Total, c.Available),
            CurrentLoan = current,
            Hold = hold,
            History = history.Select(h => new HistoryDto(h.CardNumber, h.FullName, h.LoanDate, h.ReturnedAt, h.Status.DisplayName())).ToList(),
        };
    }

    private async Task<LookupResultDto> LookupIsbnAsync(string isbn, CancellationToken ct)
    {
        var book = await _db.Books.AsNoTracking()
            .Where(b => b.Isbn == isbn)
            .Select(b => new
            {
                b.Id, b.Title, b.Isbn, b.CoverUrl, b.PublishYear, b.IsReferenceOnly,
                Category = b.Category!.Name,
                Publisher = b.Publisher != null ? b.Publisher.Name : null,
                Authors = b.BookAuthors.OrderBy(a => a.Order).Select(a => a.Author!.Name).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (book is null)
            return new LookupResultDto { Code = isbn, CodeType = "isbn", Found = false, Message = $"Chưa có đầu sách mang ISBN {isbn} trong thư viện." };

        var copies = await _db.BookCopies.AsNoTracking()
            .Where(c => c.BookId == book.Id && c.Status != CopyStatus.Withdrawn)
            .OrderBy(c => c.Barcode)
            .Select(c => new
            {
                c.Id, c.Barcode, c.Status, c.ShelfLocation,
                Due = c.LoanItems.Where(li => li.Status == LoanItemStatus.Borrowing).Select(li => (DateTime?)li.DueDate).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return new LookupResultDto
        {
            Code = isbn,
            CodeType = "isbn",
            Found = true,
            Book = new BookInfoDto(book.Id, book.Title, string.Join(", ", book.Authors), book.Isbn, book.CoverUrl, book.Category,
                book.Publisher, book.PublishYear, book.IsReferenceOnly, copies.Count, copies.Count(c => c.Status == CopyStatus.Available)),
            Copies = copies.Select(c => new CopyBriefDto(c.Id, c.Barcode, c.Status.ToString(), c.Status.DisplayName(), c.ShelfLocation, c.Due)).ToList(),
        };
    }

    private async Task<LookupResultDto> LookupTextAsync(string text, CancellationToken ct)
    {
        if (text.Length < 2)
            return new LookupResultDto { Code = text, CodeType = "text", Message = "Nhập ít nhất 2 ký tự để tìm theo nhan đề/tác giả." };
        var list = await SuggestAsync(text, 10, ct);
        return new LookupResultDto
        {
            Code = text,
            CodeType = "text",
            Found = list.Count > 0,
            Suggestions = list,
            Message = list.Count == 0 ? $"Không có đầu sách nào khớp “{text}”." : null,
        };
    }

    public async Task<List<BookSuggestDto>> SuggestAsync(string term, int take = 8, CancellationToken ct = default)
    {
        term = (term ?? "").Trim();
        if (term.Length < 2) return new();
        var digits = BarcodeRules.Normalize(term);

        var rows = await _db.Books.AsNoTracking()
            .Where(b => b.Title.Contains(term)
                     || b.BookAuthors.Any(a => a.Author!.Name.Contains(term))
                     || (b.Isbn != null && b.Isbn.StartsWith(digits)))
            .OrderBy(b => b.Title.StartsWith(term) ? 0 : 1).ThenBy(b => b.Title)
            .Take(take)
            .Select(b => new
            {
                b.Id, b.Title, b.Isbn,
                Authors = b.BookAuthors.OrderBy(a => a.Order).Select(a => a.Author!.Name).ToList(),
                Available = b.Copies.Count(c => c.Status == CopyStatus.Available),
            })
            .ToListAsync(ct);

        return rows.Select(r => new BookSuggestDto(r.Id, r.Title, string.Join(", ", r.Authors), r.Isbn, r.Available)).ToList();
    }
}
