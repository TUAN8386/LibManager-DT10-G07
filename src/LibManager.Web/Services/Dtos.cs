// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W4-01/W4-03 – CirculationService mượn – trả, FineCalculator (tuần 4, nhánh feature/tuan-luu-thong)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

namespace LibManager.Web.Services;

// ===== Kết quả dịch vụ chung =====
public class ServiceResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    /// <summary>Mã lỗi máy đọc được, ví dụ "CARD_EXPIRED", "COPY_NOT_AVAILABLE".</summary>
    public string? ErrorCode { get; init; }

    public static ServiceResult Ok(string message = "") => new() { Success = true, Message = message };
    public static ServiceResult Fail(string code, string message) => new() { Success = false, ErrorCode = code, Message = message };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }
    public static ServiceResult<T> Ok(T data, string message = "") => new() { Success = true, Data = data, Message = message };
    public static new ServiceResult<T> Fail(string code, string message) => new() { Success = false, ErrorCode = code, Message = message };
    public static ServiceResult<T> Fail(string code, string message, T data) => new() { Success = false, ErrorCode = code, Message = message, Data = data };
}

// ===== Độc giả =====
public record ReaderEligibilityDto
{
    public int Id { get; init; }
    public string CardNumber { get; init; } = "";
    public string FullName { get; init; } = "";
    public string ReaderType { get; init; } = "";
    public DateTime ExpiredAt { get; init; }
    public bool IsLocked { get; init; }
    public int CurrentLoans { get; init; }
    public int MaxBooks { get; init; }
    public int LoanDays { get; init; }
    public int OverdueCount { get; init; }
    public decimal UnpaidFines { get; init; }
    public bool CanBorrow { get; init; }
    public List<string> Reasons { get; init; } = new();
    public List<ReadyHoldDto> ReadyHolds { get; init; } = new();
    public List<ReaderLoanDto> Loans { get; init; } = new();
}

public record ReadyHoldDto(int ReservationId, string Title, string? Barcode, DateTime? ExpiresAt);

public record ReaderLoanDto(int LoanItemId, string Barcode, string Title, DateTime LoanDate, DateTime DueDate, int OverdueDays, int RenewCount);

// ===== Mượn =====
public record CopyCheckDto(int CopyId, string Barcode, string Title, string? Authors, string? Shelf, DateTime DueDate);

public record CheckoutRequest(string CardNumber, List<string> Barcodes);

public record CheckoutResultDto(int LoanId, string LoanCode, string ReaderName, DateTime LoanDate, List<CopyCheckDto> Items);

// ===== Trả =====
public record CheckinRequest(string Barcode, bool Damaged = false, string? Note = null, decimal DamageFee = 0);

public record CheckinResultDto
{
    public string Barcode { get; init; } = "";
    public string Title { get; init; } = "";
    public string ReaderName { get; init; } = "";
    public string CardNumber { get; init; } = "";
    public DateTime LoanDate { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime ReturnedAt { get; init; }
    public int OverdueDays { get; init; }
    public decimal FineAmount { get; init; }
    public int? FineId { get; init; }
    public string NewStatus { get; init; } = "";
    public string? Shelf { get; init; }
    /// <summary>Tên độc giả được giữ sách (nếu bản sao chuyển sang Đặt giữ).</summary>
    public string? HoldForReader { get; init; }
}

// ===== Tra cứu bản sao tức thời =====
public record LookupResultDto
{
    public string Code { get; init; } = "";
    public string CodeType { get; init; } = "";
    public bool Found { get; init; }
    public string? Message { get; init; }
    public CopyInfoDto? Copy { get; init; }
    public BookInfoDto? Book { get; init; }
    public CurrentLoanDto? CurrentLoan { get; init; }
    public HoldInfoDto? Hold { get; init; }
    public List<HistoryDto> History { get; init; } = new();
    public List<CopyBriefDto> Copies { get; init; } = new();
    public List<BookSuggestDto> Suggestions { get; init; } = new();
    public int ElapsedMs { get; set; }
}

public record CopyInfoDto(int Id, string Barcode, string Status, string StatusText, string? Shelf, string? Condition, DateTime AcquiredAt);
public record BookInfoDto(int Id, string Title, string? Authors, string? Isbn, string? CoverUrl, string? Category, string? Publisher, int? PublishYear, bool IsReferenceOnly, int TotalCopies, int AvailableCopies);
public record CurrentLoanDto(int LoanItemId, string CardNumber, string ReaderName, DateTime LoanDate, DateTime DueDate, int OverdueDays, int RenewCount);
public record HoldInfoDto(string CardNumber, string ReaderName, DateTime? ExpiresAt);
public record HistoryDto(string CardNumber, string ReaderName, DateTime LoanDate, DateTime? ReturnedAt, string Status);
public record CopyBriefDto(int Id, string Barcode, string Status, string StatusText, string? Shelf, DateTime? DueDate);
public record BookSuggestDto(int Id, string Title, string? Authors, string? Isbn, int AvailableCopies);

// ===== Báo cáo =====
public record LoanStatPoint(string Label, int Loans, int Returns);
public record TopBookDto(int BookId, string Title, string? Authors, int LoanCount);
public record OverdueDto(int LoanItemId, string Barcode, string Title, string CardNumber, string ReaderName, string? Email, DateTime DueDate, int OverdueDays, decimal EstimatedFine, DateTime? RemindedAt);
public record DashboardSummaryDto(int TotalBooks, int TotalCopies, int OnLoan, int Overdue, int Readers, decimal UnpaidFines, int LoansToday, int ReturnsToday);
