// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W3-03 – quản lý đầu sách, thể loại, xóa mềm (tuần 3, nhánh feature/tuan-dau-sach)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.Globalization;
using System.Text;
using LibManager.Web.Data;
using LibManager.Web.Models.Entities;
using LibManager.Web.Services;
using LibManager.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibManager.Web.Controllers;

/// <summary>Phân hệ Danh mục tài liệu và bản sao (FR-1.x) – Thủ thư, Quản trị viên.</summary>
[Authorize(Roles = Roles.Staff)]
public class BooksController : Controller
{
    private readonly LibraryDbContext _db;
    public BooksController(LibraryDbContext db) => _db = db;

    // ------------------------------------------------------------ Danh sách
    public async Task<IActionResult> Index(string? q, int? categoryId, int page = 1)
    {
        const int pageSize = 15;
        page = Math.Max(1, page);
        var query = _db.Books.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var code = BarcodeRules.Normalize(term);
            query = query.Where(b => b.Title.Contains(term) || (b.Isbn != null && b.Isbn.StartsWith(code))
                                     || b.BookAuthors.Any(a => a.Author!.Name.Contains(term))
                                     || b.Copies.Any(c => c.Barcode == code));
        }
        if (categoryId.HasValue) query = query.Where(b => b.CategoryId == categoryId);
        var total = await query.CountAsync();
        var items = await CatalogController.ToCards(query.OrderByDescending(b => b.Id).Skip((page - 1) * pageSize).Take(pageSize));
        ViewBag.Q = q;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = new SelectList(await _db.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", categoryId);
        return View(new PagedList<BookCardVm> { Items = items, Page = page, PageSize = pageSize, Total = total });
    }

    // ------------------------------------------------------------ Chi tiết + bản sao
    public async Task<IActionResult> Details(int id)
    {
        var book = await _db.Books.AsNoTracking()
            .Include(b => b.Category).Include(b => b.Publisher)
            .Include(b => b.BookAuthors).ThenInclude(a => a.Author)
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return NotFound();
        ViewBag.Due = await _db.LoanItems.AsNoTracking()
            .Where(li => li.Copy!.BookId == id && li.Status == LoanItemStatus.Borrowing)
            .ToDictionaryAsync(li => li.CopyId, li => li.DueDate);
        return View(book);
    }

    // ------------------------------------------------------------ Thêm / sửa
    public async Task<IActionResult> Create(string? isbn)
    {
        var vm = new BookFormVm { Isbn = isbn };
        await FillLookupsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(BookFormVm vm)
    {
        await ValidateBookAsync(vm);
        if (!ModelState.IsValid)
        {
            await FillLookupsAsync(vm);
            return View("Form", vm);
        }
        var book = new Book();
        await MapAsync(vm, book);
        if (vm.InitialCopies > 0)
        {
            long seq = await NextCopySequenceAsync();
            for (int i = 0; i < vm.InitialCopies; i++)
                book.Copies.Add(new BookCopy { Barcode = BarcodeRules.MakeCopyBarcode(seq++), ShelfLocation = vm.ShelfLocation, Condition = "Mới" });
        }
        _db.Books.Add(book);
        await _db.SaveChangesAsync();
        TempData["ok"] = $"Đã thêm đầu sách “{book.Title}” với {vm.InitialCopies} bản sao.";
        return RedirectToAction(nameof(Details), new { id = book.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var b = await _db.Books.Include(x => x.Publisher).Include(x => x.BookAuthors).ThenInclude(a => a.Author).FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        var vm = new BookFormVm
        {
            Id = b.Id, Title = b.Title, Isbn = b.Isbn, PublishYear = b.PublishYear, Price = b.Price, CategoryId = b.CategoryId,
            Description = b.Description, CoverUrl = b.CoverUrl, IsReferenceOnly = b.IsReferenceOnly, Publisher = b.Publisher?.Name,
            Authors = string.Join(", ", b.BookAuthors.OrderBy(a => a.Order).Select(a => a.Author!.Name)), InitialCopies = 0,
        };
        await FillLookupsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, BookFormVm vm)
    {
        if (id != vm.Id) return BadRequest();
        var book = await _db.Books.Include(x => x.BookAuthors).FirstOrDefaultAsync(x => x.Id == id);
        if (book is null) return NotFound();
        await ValidateBookAsync(vm);
        if (!ModelState.IsValid)
        {
            await FillLookupsAsync(vm);
            return View("Form", vm);
        }
        await MapAsync(vm, book);
        await _db.SaveChangesAsync();
        TempData["ok"] = "Đã cập nhật đầu sách.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var book = await _db.Books.Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return NotFound();
        if (book.Copies.Any(c => c.Status is CopyStatus.OnLoan or CopyStatus.OnHold))
        {
            TempData["err"] = "Không thể xóa: đầu sách còn bản sao đang được mượn hoặc đặt giữ.";
            return RedirectToAction(nameof(Details), new { id });
        }
        bool hasHistory = await _db.LoanItems.AnyAsync(li => li.Copy!.BookId == id);
        if (hasHistory)
        {
            book.IsDeleted = true;                                     // BR-09: xóa mềm
            foreach (var c in book.Copies) c.Status = CopyStatus.Withdrawn;
            var waiting = await _db.Reservations.Where(r => r.BookId == id && r.Status == ReservationStatus.Waiting).ToListAsync();
            foreach (var r in waiting) r.Status = ReservationStatus.Cancelled;
        }
        else
        {
            _db.BookCopies.RemoveRange(book.Copies);
            _db.Books.Remove(book);
        }
        await _db.SaveChangesAsync();
        TempData["ok"] = hasHistory ? "Đã xóa mềm đầu sách (giữ lịch sử lưu thông)." : "Đã xóa đầu sách.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ Bản sao
    [HttpPost]
    public async Task<IActionResult> AddCopies(AddCopiesVm vm)
    {
        var book = await _db.Books.FindAsync(vm.BookId);
        if (book is null) return NotFound();
        if (!ModelState.IsValid)
        {
            TempData["err"] = "Số lượng bản sao không hợp lệ (1–200).";
            return RedirectToAction(nameof(Details), new { id = vm.BookId });
        }
        long seq = await NextCopySequenceAsync();
        var created = new List<string>();
        for (int i = 0; i < vm.Count; i++)
        {
            var code = BarcodeRules.MakeCopyBarcode(seq++);
            created.Add(code);
            _db.BookCopies.Add(new BookCopy { BookId = book.Id, Barcode = code, ShelfLocation = vm.ShelfLocation, Condition = "Mới" });
        }
        await _db.SaveChangesAsync();
        TempData["ok"] = $"Đã thêm {vm.Count} bản sao: {created.First()}{(created.Count > 1 ? " … " + created.Last() : "")}.";
        TempData["printCodes"] = string.Join(",", created);
        return RedirectToAction(nameof(Details), new { id = vm.BookId });
    }

    public async Task<IActionResult> EditCopy(int id)
    {
        var c = await _db.BookCopies.Include(x => x.Book).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return View(new CopyEditVm { Id = c.Id, BookId = c.BookId, Barcode = c.Barcode, BookTitle = c.Book!.Title, ShelfLocation = c.ShelfLocation, Condition = c.Condition, Status = c.Status, RowVersion = c.RowVersion });
    }

    [HttpPost]
    public async Task<IActionResult> EditCopy(CopyEditVm vm)
    {
        var c = await _db.BookCopies.Include(x => x.Book).FirstOrDefaultAsync(x => x.Id == vm.Id);
        if (c is null) return NotFound();
        // Trạng thái lưu thông (Đang mượn, Đặt giữ) chỉ thay đổi qua nghiệp vụ mượn/trả.
        if (c.Status is CopyStatus.OnLoan or CopyStatus.OnHold && vm.Status != c.Status)
            ModelState.AddModelError(nameof(vm.Status), "Bản sao đang được mượn/đặt giữ – trạng thái chỉ đổi qua nghiệp vụ trả sách.");
        if (vm.Status is CopyStatus.OnLoan or CopyStatus.OnHold && vm.Status != c.Status)
            ModelState.AddModelError(nameof(vm.Status), "Không thể đặt thủ công trạng thái Đang mượn/Đặt giữ.");
        if (!ModelState.IsValid)
        {
            vm.Barcode = c.Barcode;
            vm.BookTitle = c.Book!.Title;
            return View(vm);
        }
        _db.Entry(c).Property(x => x.RowVersion).OriginalValue = vm.RowVersion;
        c.ShelfLocation = vm.ShelfLocation;
        c.Condition = vm.Condition;
        c.Status = vm.Status;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["err"] = "Bản sao vừa được cập nhật ở nơi khác (ví dụ: vừa cho mượn). Vui lòng tải lại và thử lại.";
            return RedirectToAction(nameof(EditCopy), new { id = vm.Id });
        }
        TempData["ok"] = $"Đã cập nhật bản sao {c.Barcode}.";
        return RedirectToAction(nameof(Details), new { id = c.BookId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCopy(int id)
    {
        var c = await _db.BookCopies.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (c.Status is CopyStatus.OnLoan or CopyStatus.OnHold)
        {
            TempData["err"] = "Bản sao đang được mượn/đặt giữ, không thể xóa.";
            return RedirectToAction(nameof(Details), new { id = c.BookId });
        }
        if (await _db.LoanItems.AnyAsync(li => li.CopyId == id))
        {
            c.Status = CopyStatus.Withdrawn;                    // BR-01, BR-09: thanh lý, không cấp lại mã
            TempData["ok"] = $"Bản sao {c.Barcode} đã có lịch sử – chuyển sang Thanh lý.";
        }
        else
        {
            _db.BookCopies.Remove(c);
            TempData["ok"] = $"Đã xóa bản sao {c.Barcode}.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = c.BookId });
    }

    // ------------------------------------------------------------ In nhãn mã vạch (FR-1.3)
    /// <summary>In nhãn cho danh sách mã (codes=BS…,BS…) hoặc toàn bộ bản sao của một đầu sách.</summary>
    public async Task<IActionResult> Labels(string? codes, int? bookId)
    {
        var q = _db.BookCopies.AsNoTracking().Include(c => c.Book).Where(c => c.Status != CopyStatus.Withdrawn);
        if (!string.IsNullOrWhiteSpace(codes))
        {
            var list = codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(BarcodeRules.Normalize).ToList();
            q = q.Where(c => list.Contains(c.Barcode));
        }
        else if (bookId.HasValue) q = q.Where(c => c.BookId == bookId);
        else q = q.OrderByDescending(c => c.Id).Take(30);

        var labels = await q.OrderBy(c => c.Barcode).Select(c => new LabelVm
        {
            Code = c.Barcode,
            Line1 = c.Book!.Title.Length > 38 ? c.Book.Title.Substring(0, 36) + "…" : c.Book.Title,
            Line2 = c.ShelfLocation,
        }).ToListAsync();
        ViewBag.Title = "In nhãn mã vạch bản sao";
        return View("~/Views/Shared/Labels.cshtml", labels);
    }

    // ------------------------------------------------------------ Nhập CSV (FR-1.4)
    public IActionResult Import() => View();

    /// <summary>
    /// CSV UTF-8, dòng đầu là tiêu đề:
    /// Title,Isbn,Authors,Publisher,Year,Price,Category,Copies,Shelf  (Authors cách nhau bằng dấu ;)
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("", "Vui lòng chọn file CSV.");
            return View();
        }
        var result = new ImportResultVm();
        var categories = await _db.Categories.ToListAsync();
        var authorCache = new Dictionary<string, Author>(StringComparer.OrdinalIgnoreCase);
        var publisherCache = new Dictionary<string, Publisher>(StringComparer.OrdinalIgnoreCase);
        long seq = await NextCopySequenceAsync();

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        string? line = await reader.ReadLineAsync(); // tiêu đề
        int row = 1;
        var seenIsbn = new HashSet<string>();
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            row++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = ParseCsvLine(line);
            if (f.Count < 7) { result.Errors.Add($"Dòng {row}: thiếu cột (cần ít nhất 7)."); continue; }
            string title = f[0].Trim();
            string? isbn = string.IsNullOrWhiteSpace(f[1]) ? null : BarcodeRules.Normalize(f[1]);
            if (title.Length == 0) { result.Errors.Add($"Dòng {row}: thiếu nhan đề."); continue; }
            if (isbn is not null && !BarcodeRules.IsValidIsbn13(isbn)) { result.Errors.Add($"Dòng {row}: ISBN {f[1]} không hợp lệ."); continue; }
            if (isbn is not null && (!seenIsbn.Add(isbn) || await _db.Books.IgnoreQueryFilters().AnyAsync(b => b.Isbn == isbn)))
            { result.Errors.Add($"Dòng {row}: ISBN {isbn} đã tồn tại – bỏ qua."); continue; }
            var cat = categories.FirstOrDefault(c => string.Equals(c.Name, f[6].Trim(), StringComparison.OrdinalIgnoreCase)
                                                  || string.Equals(c.Slug, f[6].Trim(), StringComparison.OrdinalIgnoreCase));
            if (cat is null) { result.Errors.Add($"Dòng {row}: không có thể loại “{f[6]}”."); continue; }

            var book = new Book
            {
                Title = title, Isbn = isbn, Category = cat,
                PublishYear = int.TryParse(f[4], out var y) ? y : null,
                Price = decimal.TryParse(f[5], NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : 0,
            };
            var authors = f[2].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < authors.Length; i++)
                book.BookAuthors.Add(new BookAuthor { Author = await GetAuthorAsync(authors[i], authorCache), Order = i });
            if (!string.IsNullOrWhiteSpace(f[3])) book.Publisher = await GetPublisherAsync(f[3].Trim(), publisherCache);

            int copies = f.Count > 7 && int.TryParse(f[7], out var n) ? Math.Clamp(n, 0, 100) : 1;
            string? shelf = f.Count > 8 ? f[8].Trim() : null;
            for (int i = 0; i < copies; i++)
                book.Copies.Add(new BookCopy { Barcode = BarcodeRules.MakeCopyBarcode(seq++), ShelfLocation = shelf, Condition = "Mới" });
            _db.Books.Add(book);
            result.Created++;
            result.CopiesCreated += copies;
        }
        await _db.SaveChangesAsync();
        return View("ImportResult", result);
    }

    public IActionResult ImportTemplate()
    {
        var csv = "Title,Isbn,Authors,Publisher,Year,Price,Category,Copies,Shelf\n" +
                  "\"Lập trình hướng đối tượng với C#\",9786041000018,\"Nguyễn Văn A;Trần Thị B\",NXB Giáo dục Việt Nam,2023,120000,Công nghệ thông tin,3,A3-01\n";
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", "mau-nhap-sach.csv");
    }

    // ------------------------------------------------------------ Hỗ trợ
    private async Task<long> NextCopySequenceAsync()
    {
        var last = await _db.BookCopies.IgnoreQueryFilters().OrderByDescending(c => c.Barcode).Select(c => c.Barcode).FirstOrDefaultAsync();
        return last is null ? 1 : BarcodeRules.CopySequence(last) + 1;
    }

    private async Task FillLookupsAsync(BookFormVm vm)
    {
        vm.Categories = new SelectList(await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(), "Id", "Name", vm.CategoryId);
        vm.PublisherNames = await _db.Publishers.OrderBy(p => p.Name).Select(p => p.Name).ToListAsync();
    }

    private async Task ValidateBookAsync(BookFormVm vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.Isbn))
        {
            var isbn = BarcodeRules.Normalize(vm.Isbn);
            if (!BarcodeRules.IsValidIsbn13(isbn))
                ModelState.AddModelError(nameof(vm.Isbn), "ISBN-13 sai chữ số kiểm tra.");
            else if (await _db.Books.IgnoreQueryFilters().AnyAsync(b => b.Isbn == isbn && b.Id != vm.Id))
                ModelState.AddModelError(nameof(vm.Isbn), "ISBN đã tồn tại trong thư viện.");
        }
    }

    private async Task MapAsync(BookFormVm vm, Book book)
    {
        book.Title = vm.Title.Trim();
        book.Isbn = string.IsNullOrWhiteSpace(vm.Isbn) ? null : BarcodeRules.Normalize(vm.Isbn);
        book.PublishYear = vm.PublishYear;
        book.Price = vm.Price;
        book.CategoryId = vm.CategoryId!.Value;
        book.Description = vm.Description;
        book.CoverUrl = vm.CoverUrl;
        book.IsReferenceOnly = vm.IsReferenceOnly;
        book.Publisher = string.IsNullOrWhiteSpace(vm.Publisher) ? null : await GetPublisherAsync(vm.Publisher.Trim(), new());
        if (book.Publisher is null) book.PublisherId = null;

        // Đồng bộ danh sách tác giả theo kiểu "so khớp": giữ dòng cũ, xóa dòng thừa, thêm dòng mới
        // (tránh xóa rồi thêm lại cùng khóa chính kép BookId+AuthorId).
        var names = vm.Authors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var cache = new Dictionary<string, Author>(StringComparer.OrdinalIgnoreCase);
        var desired = new List<Author>();
        foreach (var n in names) desired.Add(await GetAuthorAsync(n, cache));

        foreach (var ba in book.BookAuthors.ToList())
            if (!desired.Any(a => a.Id != 0 && a.Id == ba.AuthorId)) book.BookAuthors.Remove(ba);
        for (int i = 0; i < desired.Count; i++)
        {
            var a = desired[i];
            var existing = a.Id != 0 ? book.BookAuthors.FirstOrDefault(x => x.AuthorId == a.Id) : null;
            if (existing is not null) existing.Order = i;
            else book.BookAuthors.Add(new BookAuthor { Author = a, Order = i });
        }
    }

    private async Task<Author> GetAuthorAsync(string name, Dictionary<string, Author> cache)
    {
        if (cache.TryGetValue(name, out var a)) return a;
        a = await _db.Authors.FirstOrDefaultAsync(x => x.Name == name) ?? new Author { Name = name };
        cache[name] = a;
        return a;
    }

    private async Task<Publisher> GetPublisherAsync(string name, Dictionary<string, Publisher> cache)
    {
        if (cache.TryGetValue(name, out var p)) return p;
        p = await _db.Publishers.FirstOrDefaultAsync(x => x.Name == name) ?? new Publisher { Name = name };
        cache[name] = p;
        return p;
    }

    /// <summary>Tách một dòng CSV có hỗ trợ ngoặc kép.</summary>
    internal static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else sb.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result;
    }
}
