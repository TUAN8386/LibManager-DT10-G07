// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W4-06/W5-01 – Web API, ProblemDetails, Swagger (tuần 4, nhánh feature/hoang-api)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.Diagnostics;
using System.Security.Claims;
using LibManager.Web.Data;
using LibManager.Web.Models.Entities;
using LibManager.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibManager.Web.Controllers.Api;

/// <summary>Lớp cơ sở: chuyển ServiceResult thành HTTP 200 hoặc ProblemDetails (400/404/409).</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiBase : ControllerBase
{
    protected string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    protected IActionResult FromResult<T>(ServiceResult<T> r) =>
        r.Success ? Ok(new { success = true, message = r.Message, data = r.Data }) : ToProblem(r);

    protected IActionResult FromResult(ServiceResult r) =>
        r.Success ? Ok(new { success = true, message = r.Message }) : ToProblem(r);

    private ObjectResult ToProblem(ServiceResult r)
    {
        int status = r.ErrorCode switch
        {
            "CARD_NOT_FOUND" or "COPY_NOT_FOUND" or "NOT_FOUND" or "BOOK_NOT_FOUND" or "READER_NOT_FOUND" => 404,
            "CONFLICT" => 409,
            "FORBIDDEN" => 403,
            _ => 400,
        };
        var pd = new ProblemDetails { Status = status, Title = r.Message, Type = "https://libmanager.local/errors/" + r.ErrorCode };
        pd.Extensions["errorCode"] = r.ErrorCode;
        pd.Extensions["success"] = false;
        return new ObjectResult(pd) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}

// ======================================================================= Bản sao
/// <summary>Tra cứu bản sao tức thời theo mã quét.</summary>
[Route("api/copies")]
[Authorize(Roles = Roles.Staff)]
public class CopiesApiController : ApiBase
{
    private readonly ICopyLookupService _lookup;
    private readonly IScanLogService _scanLog;

    public CopiesApiController(ICopyLookupService lookup, IScanLogService scanLog)
    {
        _lookup = lookup;
        _scanLog = scanLog;
    }

    /// <summary>Tra cứu theo mã bản sao (BS…), ISBN-13 hoặc một phần nhan đề/tác giả.</summary>
    /// <param name="code">Chuỗi quét được hoặc gõ tay.</param>
    /// <param name="source">"scan" nếu do máy quét gửi, "typed" nếu gõ tay (ghi nhật ký).</param>
    /// <param name="ct">Hủy khi trình duyệt bỏ request (quét mã mới).</param>
    [HttpGet("lookup")]
    public async Task<ActionResult<LookupResultDto>> Lookup([FromQuery] string code, [FromQuery] string? source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new ProblemDetails { Title = "Thiếu mã cần tra cứu." });
        var result = await _lookup.LookupAsync(code, ct);
        if (source == "scan")
            await _scanLog.LogAsync(result.Code, result.CodeType, result.Found ? "FOUND" : "NOT_FOUND", result.ElapsedMs, UserId, "lookup");
        return Ok(result);
    }
}

// ======================================================================= Đầu sách (OPAC)
[Route("api/books")]
public class BooksApiController : ApiBase
{
    private readonly LibraryDbContext _db;
    private readonly ICopyLookupService _lookup;
    private readonly IIsbnInfoService _isbn;

    public BooksApiController(LibraryDbContext db, ICopyLookupService lookup, IIsbnInfoService isbn)
    {
        _db = db;
        _lookup = lookup;
        _isbn = isbn;
    }

    /// <summary>Tìm kiếm đầu sách có phân trang (công khai).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int? categoryId, [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = _db.Books.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var digits = BarcodeRules.Normalize(term);
            query = query.Where(b => b.Title.Contains(term) || b.BookAuthors.Any(a => a.Author!.Name.Contains(term))
                                     || (b.Isbn != null && b.Isbn.StartsWith(digits)));
        }
        if (categoryId.HasValue) query = query.Where(b => b.CategoryId == categoryId);

        var total = await query.CountAsync();
        var items = await query.OrderBy(b => b.Title).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.Id, b.Title, b.Isbn, b.PublishYear, b.CoverUrl, Category = b.Category!.Name,
                Authors = b.BookAuthors.OrderBy(a => a.Order).Select(a => a.Author!.Name).ToList(),
                Available = b.Copies.Count(c => c.Status == CopyStatus.Available),
                Total = b.Copies.Count(c => c.Status != CopyStatus.Withdrawn),
            }).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Gợi ý tìm kiếm tức thời (autocomplete).</summary>
    [HttpGet("suggest")]
    [AllowAnonymous]
    public async Task<ActionResult<List<BookSuggestDto>>> Suggest([FromQuery] string q, CancellationToken ct)
        => Ok(await _lookup.SuggestAsync(q, 8, ct));

    /// <summary>Lấy thông tin sách từ Open Library theo ISBN để điền nhanh biểu mẫu.</summary>
    [HttpGet("isbn-info/{isbn}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> IsbnInfo(string isbn, CancellationToken ct)
    {
        var code = BarcodeRules.Normalize(isbn);
        if (!BarcodeRules.IsValidIsbn13(code)) return BadRequest(new ProblemDetails { Title = "ISBN-13 không hợp lệ." });
        var existing = await _db.Books.AsNoTracking().Where(b => b.Isbn == code).Select(b => new { b.Id, b.Title }).FirstOrDefaultAsync(ct);
        if (existing is not null) return Ok(new { exists = true, existing.Id, existing.Title });
        var info = await _isbn.LookupAsync(code, ct);
        return info is null ? NotFound(new ProblemDetails { Title = "Không tìm thấy thông tin ISBN trên Open Library." }) : Ok(new { exists = false, info });
    }
}

// ======================================================================= Độc giả
[Route("api/readers")]
[Authorize(Roles = Roles.Staff)]
public class ReadersApiController : ApiBase
{
    private readonly ICirculationService _circ;
    private readonly IScanLogService _scanLog;

    public ReadersApiController(ICirculationService circ, IScanLogService scanLog)
    {
        _circ = circ;
        _scanLog = scanLog;
    }

    /// <summary>Thông tin độc giả và điều kiện mượn theo mã thẻ.</summary>
    [HttpGet("by-card/{cardNo}")]
    public async Task<IActionResult> ByCard(string cardNo)
    {
        var sw = Stopwatch.StartNew();
        var r = await _circ.GetReaderAsync(cardNo);
        await _scanLog.LogAsync(BarcodeRules.Normalize(cardNo), "card", r.Success ? "FOUND" : r.ErrorCode ?? "ERROR", (int)sw.ElapsedMilliseconds, UserId, "desk");
        return FromResult(r);
    }
}

// ======================================================================= Lưu thông
public record CheckCopyRequest(string CardNumber, string Barcode, List<string>? Pending);

[Route("api/circulation")]
[Authorize(Roles = Roles.Staff)]
public class CirculationApiController : ApiBase
{
    private readonly ICirculationService _circ;
    private readonly IScanLogService _scanLog;

    public CirculationApiController(ICirculationService circ, IScanLogService scanLog)
    {
        _circ = circ;
        _scanLog = scanLog;
    }

    /// <summary>Kiểm tra một bản sao vừa quét có thể thêm vào phiếu mượn.</summary>
    [HttpPost("check-copy")]
    public async Task<IActionResult> CheckCopy([FromBody] CheckCopyRequest req)
    {
        var sw = Stopwatch.StartNew();
        var r = await _circ.CheckCopyAsync(req.CardNumber, req.Barcode, req.Pending ?? new());
        await _scanLog.LogAsync(BarcodeRules.Normalize(req.Barcode), "copy", r.Success ? "OK" : r.ErrorCode ?? "ERROR", (int)sw.ElapsedMilliseconds, UserId, "desk-checkout");
        return FromResult(r);
    }

    /// <summary>Lập phiếu mượn.</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest req) => FromResult(await _circ.CheckoutAsync(req, UserId));

    /// <summary>Nhận trả một bản sao theo mã vạch.</summary>
    [HttpPost("checkin")]
    public async Task<IActionResult> Checkin([FromBody] CheckinRequest req)
    {
        var sw = Stopwatch.StartNew();
        var r = await _circ.CheckinAsync(req, UserId);
        await _scanLog.LogAsync(BarcodeRules.Normalize(req.Barcode), "copy", r.Success ? "RETURNED" : r.ErrorCode ?? "ERROR", (int)sw.ElapsedMilliseconds, UserId, "desk-checkin");
        return FromResult(r);
    }
}

// ======================================================================= Lượt mượn, phí
[Route("api")]
[Authorize]
public class LoansApiController : ApiBase
{
    private readonly ICirculationService _circ;
    private readonly IReservationService _res;
    private readonly LibraryDbContext _db;

    public LoansApiController(ICirculationService circ, IReservationService res, LibraryDbContext db)
    {
        _circ = circ;
        _res = res;
        _db = db;
    }

    private async Task<int?> CurrentReaderIdAsync()
    {
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Librarian)) return null; // nhân viên: không giới hạn
        var uid = UserId;
        var id = await _db.Readers.Where(r => r.AppUserId == uid).Select(r => (int?)r.Id).FirstOrDefaultAsync();
        return id ?? -1; // -1: tài khoản không gắn thẻ → không sở hữu lượt mượn nào
    }

    /// <summary>Gia hạn lượt mượn (độc giả chỉ gia hạn được sách của mình).</summary>
    [HttpPost("loans/{id:int}/renew")]
    public async Task<IActionResult> Renew(int id) => FromResult(await _circ.RenewAsync(id, await CurrentReaderIdAsync()));

    /// <summary>Ghi nhận mất sách.</summary>
    [HttpPost("loans/{id:int}/lost")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Lost(int id) => FromResult(await _circ.MarkLostAsync(id));

    /// <summary>Thu một khoản phí.</summary>
    [HttpPost("fines/{id:int}/pay")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> PayFine(int id) => FromResult(await _circ.PayFineAsync(id));

    /// <summary>Đặt giữ một đầu sách (độc giả).</summary>
    [HttpPost("reservations")]
    [Authorize(Roles = Roles.Reader)]
    public async Task<IActionResult> Reserve([FromBody] ReserveRequest req)
    {
        var readerId = await CurrentReaderIdAsync();
        if (readerId is null or -1) return FromResult(ServiceResult.Fail("FORBIDDEN", "Tài khoản chưa gắn với thẻ thư viện."));
        return FromResult(await _res.ReserveAsync(readerId.Value, req.BookId));
    }

    /// <summary>Hủy đặt giữ.</summary>
    [HttpPost("reservations/{id:int}/cancel")]
    public async Task<IActionResult> CancelReservation(int id) => FromResult(await _res.CancelAsync(id, await CurrentReaderIdAsync()));
}

public record ReserveRequest(int BookId);

// ======================================================================= Báo cáo
[Route("api/reports")]
[Authorize(Roles = Roles.Staff)]
public class ReportsApiController : ApiBase
{
    private readonly IReportService _reports;
    private readonly LibraryDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly TimeProvider _time;

    public ReportsApiController(IReportService reports, LibraryDbContext db, IAppEmailSender email, TimeProvider time)
    {
        _reports = reports;
        _db = db;
        _email = email;
        _time = time;
    }

    private (DateTime from, DateTime to) Range(DateTime? from, DateTime? to)
    {
        var today = _time.GetLocalNow().DateTime.Date;
        var t = to?.Date ?? today;
        var f = from?.Date ?? t.AddDays(-29);
        if (f > t) (f, t) = (t, f);
        if ((t - f).TotalDays > 731) f = t.AddDays(-731);
        return (f, t);
    }

    /// <summary>Số liệu tổng quan cho dashboard.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary() => Ok(await _reports.GetSummaryAsync());

    /// <summary>Lượt mượn – trả theo thời gian (biểu đồ).</summary>
    [HttpGet("loans")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<LoanStatPoint>>> Loans([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string groupBy = "day")
    {
        var (f, t) = Range(from, to);
        return Ok(await _reports.GetLoanStatsAsync(f, t, groupBy));
    }

    /// <summary>Top đầu sách được mượn nhiều.</summary>
    [HttpGet("top-books")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<TopBookDto>>> TopBooks([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int take = 10)
    {
        var (f, t) = Range(from, to);
        return Ok(await _reports.GetTopBooksAsync(f, t, Math.Clamp(take, 1, 50)));
    }

    /// <summary>Lượt mượn theo thể loại.</summary>
    [HttpGet("categories")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Categories([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (f, t) = Range(from, to);
        var rows = await _reports.GetCategoryStatsAsync(f, t);
        return Ok(rows.Select(r => new { category = r.Category, loans = r.Loans }));
    }

    /// <summary>Danh sách sách quá hạn.</summary>
    [HttpGet("overdue")]
    public async Task<ActionResult<List<OverdueDto>>> Overdue([FromQuery] string? q) => Ok(await _reports.GetOverdueAsync(q));

    /// <summary>Gửi email nhắc trả cho một lượt mượn quá hạn.</summary>
    [HttpPost("overdue/{loanItemId:int}/remind")]
    public async Task<IActionResult> Remind(int loanItemId)
    {
        var now = _time.GetLocalNow().DateTime;
        var li = await _db.LoanItems
            .Include(x => x.Copy!).ThenInclude(c => c.Book)
            .Include(x => x.Loan!).ThenInclude(l => l.Reader!).ThenInclude(r => r.ReaderType)
            .FirstOrDefaultAsync(x => x.Id == loanItemId && x.Status == LoanItemStatus.Borrowing);
        if (li is null) return FromResult(ServiceResult.Fail("NOT_FOUND", "Không tìm thấy lượt mượn đang mở."));
        var reader = li.Loan!.Reader!;
        if (string.IsNullOrWhiteSpace(reader.Email)) return FromResult(ServiceResult.Fail("NO_EMAIL", "Độc giả chưa có email."));
        var days = FineCalculator.OverdueDays(li.DueDate, now);
        await _email.SendAsync(reader.Email, reader.FullName, "[Thư viện] Sách đã quá hạn trả",
            EmailTemplates.OverdueReminder(reader.FullName, li.Copy!.Book!.Title, li.DueDate, days,
                FineCalculator.OverdueFine(days, reader.ReaderType!.FinePerDay, li.Copy.Book.Price)));
        li.RemindedAt = now;
        await _db.SaveChangesAsync();
        return FromResult(ServiceResult.Ok($"Đã gửi email nhắc tới {reader.Email}."));
    }
}
