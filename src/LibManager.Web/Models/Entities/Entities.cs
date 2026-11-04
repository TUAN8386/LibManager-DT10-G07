// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W2-05 – entity, LibraryDbContext, dữ liệu mẫu (tuần 2, nhánh feature/tuan-csdl)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace LibManager.Web.Models.Entities;

/// <summary>Người dùng hệ thống (mở rộng ASP.NET Core Identity).</summary>
public class AppUser : IdentityUser
{
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Reader? Reader { get; set; }
}

/// <summary>Thể loại sách.</summary>
public class Category
{
    public int Id { get; set; }
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required, StringLength(120)]
    public string Slug { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

/// <summary>Tác giả.</summary>
public class Author
{
    public int Id { get; set; }
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Bio { get; set; }
    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
}

/// <summary>Nhà xuất bản.</summary>
public class Publisher
{
    public int Id { get; set; }
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
    [StringLength(250)]
    public string? Address { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

/// <summary>Đầu sách (một tác phẩm/ấn phẩm).</summary>
public class Book
{
    public int Id { get; set; }
    [Required, StringLength(250)]
    public string Title { get; set; } = string.Empty;
    /// <summary>ISBN-13, chỉ chữ số. Có thể null với tài liệu không có ISBN.</summary>
    [StringLength(13)]
    public string? Isbn { get; set; }
    public int? PublishYear { get; set; }
    /// <summary>Giá bìa (VNĐ) – dùng tính trần phí phạt và bồi thường.</summary>
    [Column(TypeName = "decimal(18,0)")]
    public decimal Price { get; set; }
    [StringLength(4000)]
    public string? Description { get; set; }
    [StringLength(500)]
    public string? CoverUrl { get; set; }
    /// <summary>Sách chỉ đọc tại chỗ, không cho mượn về.</summary>
    public bool IsReferenceOnly { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? PublisherId { get; set; }
    public Publisher? Publisher { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

/// <summary>Bảng liên kết nhiều-nhiều Đầu sách – Tác giả.</summary>
public class BookAuthor
{
    public int BookId { get; set; }
    public Book? Book { get; set; }
    public int AuthorId { get; set; }
    public Author? Author { get; set; }
    public int Order { get; set; }
}

public enum CopyStatus
{
    [Display(Name = "Sẵn sàng")] Available = 0,
    [Display(Name = "Đang mượn")] OnLoan = 1,
    [Display(Name = "Đặt giữ")] OnHold = 2,
    [Display(Name = "Hư hỏng")] Damaged = 3,
    [Display(Name = "Mất")] Lost = 4,
    [Display(Name = "Thanh lý")] Withdrawn = 5,
}

/// <summary>Bản sao vật lý – mỗi bản sao một mã vạch duy nhất.</summary>
public class BookCopy
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book? Book { get; set; }
    /// <summary>Mã vạch: "BS" + 9 chữ số + 1 chữ số kiểm tra Luhn (12 ký tự).</summary>
    [Required, StringLength(12)]
    public string Barcode { get; set; } = string.Empty;
    [StringLength(50)]
    public string? ShelfLocation { get; set; }
    public CopyStatus Status { get; set; } = CopyStatus.Available;
    [StringLength(250)]
    public string? Condition { get; set; }
    public DateTime AcquiredAt { get; set; } = DateTime.Now;
    /// <summary>Concurrency token – chống hai quầy thao tác cùng bản sao.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<LoanItem> LoanItems { get; set; } = new List<LoanItem>();
}

/// <summary>Loại độc giả và quy định mượn tương ứng.</summary>
public class ReaderType
{
    public int Id { get; set; }
    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;
    [Range(1, 50)]
    public int MaxBooks { get; set; } = 5;
    [Range(1, 365)]
    public int LoanDays { get; set; } = 14;
    [Range(0, 10)]
    public int MaxRenewals { get; set; } = 2;
    [Column(TypeName = "decimal(18,0)")]
    [Range(0, 1_000_000)]
    public decimal FinePerDay { get; set; } = 2000;
    public ICollection<Reader> Readers { get; set; } = new List<Reader>();
}

/// <summary>Độc giả và thẻ thư viện.</summary>
public class Reader
{
    public int Id { get; set; }
    /// <summary>Mã thẻ: "DG" + 8 chữ số.</summary>
    [Required, StringLength(10)]
    public string CardNumber { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;
    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }
    [Phone, StringLength(20)]
    public string? Phone { get; set; }
    public int ReaderTypeId { get; set; }
    public ReaderType? ReaderType { get; set; }
    public DateTime ExpiredAt { get; set; }
    public bool IsLocked { get; set; }
    public string? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

/// <summary>Phiếu mượn (một lượt giao dịch tại quầy, nhiều bản sao).</summary>
public class Loan
{
    public int Id { get; set; }
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;
    public int ReaderId { get; set; }
    public Reader? Reader { get; set; }
    public string? LibrarianId { get; set; }
    public AppUser? Librarian { get; set; }
    public DateTime LoanDate { get; set; } = DateTime.Now;
    public ICollection<LoanItem> Items { get; set; } = new List<LoanItem>();
}

public enum LoanItemStatus
{
    [Display(Name = "Đang mượn")] Borrowing = 0,
    [Display(Name = "Đã trả")] Returned = 1,
    [Display(Name = "Mất")] Lost = 2,
}

/// <summary>Chi tiết phiếu mượn – mỗi dòng một bản sao.</summary>
public class LoanItem
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public Loan? Loan { get; set; }
    public int CopyId { get; set; }
    public BookCopy? Copy { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public int RenewCount { get; set; }
    public LoanItemStatus Status { get; set; } = LoanItemStatus.Borrowing;
    public DateTime? RemindedAt { get; set; }
}

public enum ReservationStatus
{
    [Display(Name = "Đang chờ")] Waiting = 0,
    [Display(Name = "Sách đã sẵn sàng")] Ready = 1,
    [Display(Name = "Đã nhận sách")] Fulfilled = 2,
    [Display(Name = "Đã hủy")] Cancelled = 3,
    [Display(Name = "Hết hạn giữ")] Expired = 4,
}

/// <summary>Yêu cầu đặt giữ một đầu sách (hàng đợi FIFO).</summary>
public class Reservation
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book? Book { get; set; }
    public int ReaderId { get; set; }
    public Reader? Reader { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Waiting;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ReadyAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    /// <summary>Bản sao đang được giữ cho yêu cầu này (khi Status = Ready).</summary>
    public int? CopyId { get; set; }
    public BookCopy? Copy { get; set; }
}

public enum FineReason
{
    [Display(Name = "Quá hạn")] Overdue = 0,
    [Display(Name = "Hư hỏng")] Damaged = 1,
    [Display(Name = "Mất sách")] Lost = 2,
}

/// <summary>Phí phạt / bồi thường.</summary>
public class Fine
{
    public int Id { get; set; }
    public int ReaderId { get; set; }
    public Reader? Reader { get; set; }
    public int? LoanItemId { get; set; }
    public LoanItem? LoanItem { get; set; }
    [Column(TypeName = "decimal(18,0)")]
    public decimal Amount { get; set; }
    public FineReason Reason { get; set; }
    public bool IsPaid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? PaidAt { get; set; }
    [StringLength(250)]
    public string? Note { get; set; }
}

/// <summary>Nhật ký mỗi lượt quét/tra cứu mã.</summary>
public class ScanLog
{
    public long Id { get; set; }
    [Required, StringLength(64)]
    public string Code { get; set; } = string.Empty;
    [StringLength(20)]
    public string CodeType { get; set; } = string.Empty;
    [StringLength(30)]
    public string Result { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string? UserId { get; set; }
    [StringLength(50)]
    public string? Workstation { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.Now;
}
