// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W2-05 – entity, LibraryDbContext, dữ liệu mẫu (tuần 2, nhánh feature/tuan-csdl)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.Text.Json;
using LibManager.Web.Models.Entities;
using LibManager.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LibManager.Web.Data;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Librarian = "Librarian";
    public const string Reader = "Reader";
    public const string Staff = Admin + "," + Librarian;
}

/// <summary>
/// Tạo dữ liệu mẫu từ Data/SeedData/library-seed.json (dùng chung với công cụ in nhãn mã vạch,
/// nên mã vạch trên nhãn in khớp với CSDL). Mã bản sao sinh tuần tự BS000000001x theo thứ tự đầu sách.
/// </summary>
public static class DbSeeder
{
    private record SeedFile(List<SeedCategory> Categories, List<SeedReaderType> ReaderTypes, List<SeedBook> Books, List<SeedReader> Readers);
    private record SeedCategory(string Slug, string Name, string ShelfPrefix);
    private record SeedReaderType(string Code, string Name, int MaxBooks, int LoanDays, int MaxRenewals, decimal FinePerDay);
    private record SeedBook(string Title, List<string> Authors, string Category, string Publisher, int Year, decimal Price,
        int Copies, bool ReferenceOnly, string Isbn, string Shelf);
    private record SeedReader(string Card, string Name, string Type, string Email, string Phone, int ValidDays, bool Locked);

    public static async Task SeedAsync(IServiceProvider sp, ILogger logger)
    {
        var db = sp.GetRequiredService<LibraryDbContext>();
        var userMgr = sp.GetRequiredService<UserManager<AppUser>>();
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var now = sp.GetRequiredService<TimeProvider>().GetLocalNow().DateTime;

        foreach (var r in new[] { Roles.Admin, Roles.Librarian, Roles.Reader })
            if (!await roleMgr.RoleExistsAsync(r)) await roleMgr.CreateAsync(new IdentityRole(r));

        var admin = await EnsureUserAsync(userMgr, "admin@libmanager.local", "Quản trị viên", "Admin@123", Roles.Admin);
        var librarian = await EnsureUserAsync(userMgr, "thuthu@libmanager.local", "Thủ thư Nguyễn Thị Hằng", "Thuthu@123", Roles.Librarian);

        if (await db.Books.IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("CSDL đã có dữ liệu – bỏ qua seed.");
            return;
        }

        var path = Path.Combine(env.ContentRootPath, "Data", "SeedData", "library-seed.json");
        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // ---- Danh mục, loại độc giả
        var cats = seed.Categories.ToDictionary(c => c.Slug, c => new Category { Name = c.Name, Slug = c.Slug });
        db.Categories.AddRange(cats.Values);
        var types = seed.ReaderTypes.ToDictionary(t => t.Code, t => new ReaderType
        {
            Name = t.Name, MaxBooks = t.MaxBooks, LoanDays = t.LoanDays, MaxRenewals = t.MaxRenewals, FinePerDay = t.FinePerDay,
        });
        db.ReaderTypes.AddRange(types.Values);

        // ---- Tác giả, NXB, đầu sách, bản sao
        var authors = new Dictionary<string, Author>();
        var publishers = new Dictionary<string, Publisher>();
        var books = new List<Book>();
        long seq = 1;
        foreach (var sb in seed.Books)
        {
            if (!BarcodeRules.IsValidIsbn13(sb.Isbn)) throw new InvalidOperationException($"ISBN sai: {sb.Isbn}");
            if (!publishers.TryGetValue(sb.Publisher, out var pub))
                publishers[sb.Publisher] = pub = new Publisher { Name = sb.Publisher };
            var book = new Book
            {
                Title = sb.Title, Isbn = sb.Isbn, PublishYear = sb.Year, Price = sb.Price, IsReferenceOnly = sb.ReferenceOnly,
                Category = cats[sb.Category], Publisher = pub, CreatedAt = now.AddDays(-200),
                Description = $"{sb.Title} – {string.Join(", ", sb.Authors)}. Tài liệu mẫu phục vụ demo hệ thống LibManager.",
            };
            for (int i = 0; i < sb.Authors.Count; i++)
            {
                if (!authors.TryGetValue(sb.Authors[i], out var au))
                    authors[sb.Authors[i]] = au = new Author { Name = sb.Authors[i] };
                book.BookAuthors.Add(new BookAuthor { Author = au, Order = i });
            }
            for (int i = 0; i < sb.Copies; i++)
                book.Copies.Add(new BookCopy
                {
                    Barcode = BarcodeRules.MakeCopyBarcode(seq++), ShelfLocation = sb.Shelf,
                    Status = CopyStatus.Available, AcquiredAt = now.AddDays(-180), Condition = "Tốt",
                });
            books.Add(book);
        }
        db.Books.AddRange(books);

        // ---- Độc giả
        var readers = seed.Readers.Select(r => new Reader
        {
            CardNumber = r.Card, FullName = r.Name, ReaderType = types[r.Type], Email = r.Email, Phone = r.Phone,
            ExpiredAt = now.Date.AddDays(r.ValidDays), IsLocked = r.Locked, CreatedAt = now.AddDays(-120),
        }).ToList();
        db.Readers.AddRange(readers);
        await db.SaveChangesAsync();

        // Tài khoản độc giả mẫu gắn với thẻ DG24000001
        var readerUser = await EnsureUserAsync(userMgr, "docgia@libmanager.local", readers[0].FullName, "Docgia@123", Roles.Reader);
        readers[0].AppUserId = readerUser.Id;

        // ---- Lịch sử lưu thông (đã trả) cho dashboard: ~90 ngày gần nhất
        var rnd = new Random(2410);
        BookCopy Copy(int bookIdx, int n = 0) => books[bookIdx].Copies.ElementAt(n);
        var reserved = new HashSet<BookCopy> { Copy(6), Copy(24), Copy(18), Copy(0), Copy(3), Copy(9), Copy(13), Copy(1), Copy(27) };
        var circulating = books.Where(b => !b.IsReferenceOnly).SelectMany(b => b.Copies).Where(c => !reserved.Contains(c)).ToList();
        var activeReaders = readers.Where(r => !r.IsLocked && r.ExpiredAt > now).ToList();

        for (int i = 0; i < 120; i++)
        {
            var reader = activeReaders[rnd.Next(activeReaders.Count)];
            var loanDate = now.Date.AddDays(-rnd.Next(5, 90)).AddHours(8 + rnd.Next(0, 9)).AddMinutes(rnd.Next(0, 60));
            var loan = new Loan { Code = $"PM{loanDate:yyMMddHHmm}{i:D3}", Reader = reader, LibrarianId = librarian.Id, LoanDate = loanDate };
            int count = rnd.Next(1, 3);
            for (int k = 0; k < count; k++)
            {
                var copy = circulating[rnd.Next(circulating.Count)];
                var due = FineCalculator.DueDate(loanDate, reader.ReaderType!.LoanDays);
                var returned = loanDate.AddDays(rnd.Next(2, reader.ReaderType.LoanDays + 6)).AddHours(rnd.Next(0, 5));
                if (returned > now) returned = now.AddHours(-1);
                var item = new LoanItem { Copy = copy, DueDate = due, ReturnedAt = returned, Status = LoanItemStatus.Returned };
                loan.Items.Add(item);
                var late = FineCalculator.OverdueDays(due, returned);
                if (late > 0)
                    db.Fines.Add(new Fine
                    {
                        Reader = reader, LoanItem = item, Reason = FineReason.Overdue, CreatedAt = returned, IsPaid = true, PaidAt = returned,
                        Amount = FineCalculator.OverdueFine(late, reader.ReaderType.FinePerDay, copy.Book?.Price ?? 0),
                        Note = $"Quá hạn {late} ngày",
                    });
            }
            db.Loans.Add(loan);
        }

        // ---- Tình huống demo đang mở (khớp với kịch bản kiểm thử DT10-05)
        void OpenLoan(Reader reader, int daysAgo, params BookCopy[] copies)
        {
            var date = now.Date.AddDays(-daysAgo).AddHours(9);
            var loan = new Loan { Code = $"PM{date:yyMMdd}{reader.CardNumber[^4..]}{daysAgo:D2}", Reader = reader, LibrarianId = librarian.Id, LoanDate = date };
            foreach (var c in copies)
            {
                c.Status = CopyStatus.OnLoan;
                loan.Items.Add(new LoanItem { Copy = c, DueDate = FineCalculator.DueDate(date, reader.ReaderType!.LoanDays) });
            }
            db.Loans.Add(loan);
        }
        Reader R(string card) => readers.First(r => r.CardNumber == card);

        OpenLoan(R("DG24000001"), 5, Copy(6), Copy(24));             // An: đang mượn 2 cuốn, còn hạn
        OpenLoan(R("DG24000002"), 20, Copy(18));                     // Bình: quá hạn 6 ngày → không được mượn
        OpenLoan(R("DG24000003"), 10, Copy(0), Copy(3), Copy(9));    // Châu (GV): Clean Code (bản duy nhất)
        OpenLoan(R("DG24000008"), 29, Copy(1));                      // Mai (GV): còn 1 ngày đến hạn → email nhắc
        OpenLoan(R("DG24000010"), 16, Copy(27));                     // Linh: quá hạn 2 ngày

        // An đặt giữ Clean Code (bản sao duy nhất đang được Châu mượn)
        db.Reservations.Add(new Reservation { Book = books[0], Reader = R("DG24000001"), CreatedAt = now.AddDays(-2) });

        // Lan có sách đặt giữ đã sẵn sàng: Code Complete
        var held = Copy(13);
        held.Status = CopyStatus.OnHold;
        db.Reservations.Add(new Reservation
        {
            Book = books[13], Reader = R("DG24000007"), Status = ReservationStatus.Ready, Copy = held,
            CreatedAt = now.AddDays(-6), ReadyAt = now.AddDays(-1), ExpiresAt = now.Date.AddDays(3).AddTicks(-1),
        });

        // Huy nợ phí 60.000 đ (vượt ngưỡng 50.000 đ) → không được mượn
        db.Fines.Add(new Fine { Reader = R("DG24000006"), Amount = 60_000, Reason = FineReason.Damaged, CreatedAt = now.AddDays(-15), Note = "Làm rách bìa sách" });

        // Một bản sao hư hỏng, một bản sao mất để demo trạng thái
        books[21].Copies.ElementAt(3).Status = CopyStatus.Damaged;
        books[21].Copies.ElementAt(3).Condition = "Bong gáy, chờ sửa";
        books[30].Copies.ElementAt(3).Status = CopyStatus.Lost;

        await db.SaveChangesAsync();
        logger.LogInformation("Đã seed {Books} đầu sách, {Copies} bản sao, {Readers} độc giả.", books.Count, seq - 1, readers.Count);
    }

    private static async Task<AppUser> EnsureUserAsync(UserManager<AppUser> userMgr, string email, string fullName, string password, string role)
    {
        var user = await userMgr.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser { UserName = email, Email = email, FullName = fullName, EmailConfirmed = true };
            var res = await userMgr.CreateAsync(user, password);
            if (!res.Succeeded) throw new InvalidOperationException(string.Join("; ", res.Errors.Select(e => e.Description)));
        }
        if (!await userMgr.IsInRoleAsync(user, role)) await userMgr.AddToRoleAsync(user, role);
        return user;
    }

    /// <summary>
    /// Sinh dữ liệu kiểm thử hiệu năng: thêm đầu sách giả, mỗi đầu sách nhiều bản sao, tới khi đạt targetCopies.
    /// Dùng để đo NFR-08 (tra cứu &lt; 300 ms với 50.000 bản sao).
    /// </summary>
    public static async Task<int> GeneratePerformanceDataAsync(LibraryDbContext db, int targetCopies, CancellationToken ct = default)
    {
        int existing = await db.BookCopies.IgnoreQueryFilters().CountAsync(ct);
        if (existing >= targetCopies) return 0;

        var lastBarcode = await db.BookCopies.IgnoreQueryFilters().OrderByDescending(c => c.Barcode).Select(c => c.Barcode).FirstOrDefaultAsync(ct);
        long seq = lastBarcode is null ? 1 : BarcodeRules.CopySequence(lastBarcode) + 1;
        var categoryIds = await db.Categories.Select(c => c.Id).ToListAsync(ct);
        var author = new Author { Name = "Tác giả mẫu (dữ liệu hiệu năng)" };
        db.Authors.Add(author);
        await db.SaveChangesAsync(ct);

        int toCreate = targetCopies - existing, created = 0, n = 0;
        var oldDetect = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            while (created < toCreate)
            {
                for (int b = 0; b < 500 && created < toCreate; b++, n++)
                {
                    var book = new Book
                    {
                        Title = $"Tài liệu kiểm thử hiệu năng số {n + 1:D5}", Price = 100_000,
                        CategoryId = categoryIds[n % categoryIds.Count],
                    };
                    book.BookAuthors.Add(new BookAuthor { AuthorId = author.Id });
                    for (int k = 0; k < 5 && created < toCreate; k++, created++)
                        book.Copies.Add(new BookCopy { Barcode = BarcodeRules.MakeCopyBarcode(seq++), ShelfLocation = $"Z{n % 50 + 1}-{k + 1:D2}" });
                    db.Books.Add(book);
                }
                db.ChangeTracker.DetectChanges();
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = oldDetect;
        }
        return created;
    }
}
