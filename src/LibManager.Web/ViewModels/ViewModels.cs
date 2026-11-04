// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W2-05 – entity, LibraryDbContext, dữ liệu mẫu (tuần 2, nhánh feature/tuan-csdl)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.ComponentModel.DataAnnotations;
using LibManager.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LibManager.Web.ViewModels;

/// <summary>Danh sách có phân trang dùng chung.</summary>
public class PagedList<T>
{
    public List<T> Items { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
    public int TotalPages => (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize));
}

// ================================================================ Tài khoản
public class LoginVm
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";
    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordVm
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = "";
}

public class ResetPasswordVm
{
    [Required] public string Email { get; set; } = "";
    [Required] public string Token { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới."), DataType(DataType.Password), StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 ký tự.")]
    [Display(Name = "Mật khẩu mới")]
    public string Password { get; set; } = "";
    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";
}

/// <summary>Độc giả tự đăng ký tài khoản trực tuyến (thẻ ở trạng thái khóa, chờ thủ thư xác minh tại quầy).</summary>
public class RegisterVm
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 100 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(150)]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại."), RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại gồm 10–11 chữ số, bắt đầu bằng 0.")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại độc giả.")]
    [Display(Name = "Loại độc giả")]
    public int ReaderTypeId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), DataType(DataType.Password), StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 ký tự.")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";
    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";
    [Display(Name = "Tôi đã đọc và đồng ý với nội quy thư viện")]
    public bool AcceptRules { get; set; }

    public List<SelectListItem> ReaderTypes { get; set; } = new();
}

public class ActivateVm
{
    [Required(ErrorMessage = "Vui lòng nhập mã thẻ."), RegularExpression(@"^[Dd][Gg]\d{8}$", ErrorMessage = "Mã thẻ có dạng DG + 8 chữ số.")]
    [Display(Name = "Mã thẻ thư viện")]
    public string CardNumber { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email đã đăng ký với thư viện")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), DataType(DataType.Password), StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 ký tự.")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";
    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";
}

public class ProfileVm
{
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100)]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ."), Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }
    public string? CardNumber { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}

public class ChangePasswordVm
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại."), DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới."), DataType(DataType.Password), StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 ký tự."), Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = "";
    [DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp."), Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = "";
}

// ================================================================ Đầu sách & bản sao
public class BookFormVm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập nhan đề."), StringLength(250), Display(Name = "Nhan đề")]
    public string Title { get; set; } = "";
    [Display(Name = "ISBN-13"), RegularExpression(@"^[\d\- ]{13,17}$", ErrorMessage = "ISBN gồm 13 chữ số (có thể có dấu gạch).")]
    public string? Isbn { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập ít nhất một tác giả."), Display(Name = "Tác giả (cách nhau bằng dấu phẩy)")]
    public string Authors { get; set; } = "";
    [Display(Name = "Nhà xuất bản"), StringLength(150)]
    public string? Publisher { get; set; }
    [Display(Name = "Năm xuất bản"), Range(1400, 2100, ErrorMessage = "Năm không hợp lệ.")]
    public int? PublishYear { get; set; }
    [Display(Name = "Giá bìa (đ)"), Range(0, 100_000_000, ErrorMessage = "Giá không hợp lệ.")]
    public decimal Price { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn thể loại."), Display(Name = "Thể loại")]
    public int? CategoryId { get; set; }
    [Display(Name = "Mô tả"), StringLength(4000)]
    public string? Description { get; set; }
    [Display(Name = "Ảnh bìa (URL)"), StringLength(500), Url(ErrorMessage = "URL không hợp lệ.")]
    public string? CoverUrl { get; set; }
    [Display(Name = "Chỉ đọc tại chỗ")]
    public bool IsReferenceOnly { get; set; }
    [Display(Name = "Số bản sao tạo kèm"), Range(0, 100)]
    public int InitialCopies { get; set; } = 1;
    [Display(Name = "Vị trí kệ"), StringLength(50)]
    public string? ShelfLocation { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<string> PublisherNames { get; set; } = Enumerable.Empty<string>();
}

public class AddCopiesVm
{
    public int BookId { get; set; }
    [Range(1, 200, ErrorMessage = "Số lượng từ 1 đến 200.")]
    public int Count { get; set; } = 1;
    [StringLength(50)]
    public string? ShelfLocation { get; set; }
}

public class CopyEditVm
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string Barcode { get; set; } = "";
    public string BookTitle { get; set; } = "";
    [Display(Name = "Vị trí kệ"), StringLength(50)]
    public string? ShelfLocation { get; set; }
    [Display(Name = "Tình trạng"), StringLength(250)]
    public string? Condition { get; set; }
    [Display(Name = "Trạng thái")]
    public CopyStatus Status { get; set; }
    public byte[]? RowVersion { get; set; }
}

public class LabelVm
{
    public string Code { get; set; } = "";
    public string Line1 { get; set; } = "";
    public string? Line2 { get; set; }
}

public class ImportResultVm
{
    public int Created { get; set; }
    public int CopiesCreated { get; set; }
    public List<string> Errors { get; set; } = new();
}

// ================================================================ Độc giả
public class ReaderFormVm
{
    public int Id { get; set; }
    [Display(Name = "Mã thẻ")]
    public string? CardNumber { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100), Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";
    [EmailAddress(ErrorMessage = "Email không hợp lệ."), StringLength(150), Display(Name = "Email")]
    public string? Email { get; set; }
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ."), StringLength(20), Display(Name = "Điện thoại")]
    public string? Phone { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn loại độc giả."), Display(Name = "Loại độc giả")]
    public int? ReaderTypeId { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Ngày hết hạn thẻ")]
    public DateTime ExpiredAt { get; set; } = DateTime.Today.AddYears(1);
    [Display(Name = "Khóa thẻ")]
    public bool IsLocked { get; set; }
    public IEnumerable<SelectListItem> ReaderTypes { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class ReaderDetailsVm
{
    public Reader Reader { get; set; } = null!;
    public List<LoanItem> OpenLoans { get; set; } = new();
    public List<LoanItem> History { get; set; } = new();
    public List<Fine> Fines { get; set; } = new();
    public List<Reservation> Reservations { get; set; } = new();
    public decimal Unpaid => Fines.Where(f => !f.IsPaid).Sum(f => f.Amount);
}

// ================================================================ Quản trị
public class UserRowVm
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public IList<string> Roles { get; set; } = new List<string>();
    public bool IsLockedOut { get; set; }
    public string? CardNumber { get; set; }
}

public class CreateStaffVm
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";
    [Required, Display(Name = "Vai trò")]
    public string Role { get; set; } = "Librarian";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), DataType(DataType.Password), StringLength(100, MinimumLength = 6), Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";
}

// ================================================================ Trang chủ / OPAC
public class HomeVm
{
    public DashboardStat Stats { get; set; } = new();
    public List<BookCardVm> NewBooks { get; set; } = new();
    public List<BookCardVm> Popular { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
}

public class DashboardStat
{
    public int Books { get; set; }
    public int Copies { get; set; }
    public int Available { get; set; }
    public int Readers { get; set; }
}

public class BookCardVm
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Authors { get; set; } = "";
    public string? CoverUrl { get; set; }
    public string? Category { get; set; }
    public int? PublishYear { get; set; }
    public int Available { get; set; }
    public int Total { get; set; }
    public bool IsReferenceOnly { get; set; }
}

public class CatalogIndexVm
{
    public string? Q { get; set; }
    public int? CategoryId { get; set; }
    public string Sort { get; set; } = "title";
    public bool AvailableOnly { get; set; }
    public PagedList<BookCardVm> Result { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
}

public class CatalogDetailsVm
{
    public Book Book { get; set; } = null!;
    public string Authors { get; set; } = "";
    public List<CopyRowVm> Copies { get; set; } = new();
    public DateTime? NextDueDate { get; set; }
    public int WaitingCount { get; set; }
    public bool CanReserve { get; set; }
    public string? ReserveBlockedReason { get; set; }
}

public class CopyRowVm
{
    public int Id { get; set; }
    public string Barcode { get; set; } = "";
    public string? Shelf { get; set; }
    public CopyStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Condition { get; set; }
}

public class MyLibraryVm
{
    public Reader Reader { get; set; } = null!;
    public List<LoanItem> OpenLoans { get; set; } = new();
    public List<LoanItem> History { get; set; } = new();
    public List<Reservation> Reservations { get; set; } = new();
    public List<Fine> Fines { get; set; } = new();
    public Dictionary<int, int> QueuePositions { get; set; } = new();
}
