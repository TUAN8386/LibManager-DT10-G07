// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W2-05 – entity, LibraryDbContext, dữ liệu mẫu (tuần 2, nhánh feature/tuan-csdl)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using LibManager.Web.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LibManager.Web.Data;

public class LibraryDbContext : IdentityDbContext<AppUser>
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();
    public DbSet<ReaderType> ReaderTypes => Set<ReaderType>();
    public DbSet<Reader> Readers => Set<Reader>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanItem> LoanItems => Set<LoanItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Fine> Fines => Set<Fine>();
    public DbSet<ScanLog> ScanLogs => Set<ScanLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Book>(e =>
        {
            // Collation tiếng Việt không phân biệt dấu: tìm "lap trinh" vẫn ra "Lập trình".
            e.Property(x => x.Title).UseCollation("Vietnamese_CI_AI");
            e.Property(x => x.Isbn).IsUnicode(false);
            e.HasIndex(x => x.Isbn).IsUnique().HasFilter("[Isbn] IS NOT NULL");
            e.HasIndex(x => x.Title);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.Category).WithMany(c => c.Books).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Publisher).WithMany(p => p.Books).HasForeignKey(x => x.PublisherId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Author>(e => e.Property(x => x.Name).UseCollation("Vietnamese_CI_AI"));

        b.Entity<BookAuthor>(e =>
        {
            e.HasKey(x => new { x.BookId, x.AuthorId });
            e.HasOne(x => x.Book).WithMany(x => x.BookAuthors).HasForeignKey(x => x.BookId);
            e.HasOne(x => x.Author).WithMany(x => x.BookAuthors).HasForeignKey(x => x.AuthorId);
            e.HasQueryFilter(x => !x.Book!.IsDeleted);   // khớp bộ lọc xóa mềm của Book (tránh cảnh báo EF 10622)
        });

        b.Entity<BookCopy>(e =>
        {
            e.Property(x => x.Barcode).IsUnicode(false);
            e.HasIndex(x => x.Barcode).IsUnique();          // tra cứu tức thời theo mã vạch
            e.HasIndex(x => new { x.BookId, x.Status });
            e.HasOne(x => x.Book).WithMany(x => x.Copies).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.Book!.IsDeleted);
        });

        b.Entity<Reader>(e =>
        {
            e.Property(x => x.CardNumber).IsUnicode(false);
            e.Property(x => x.FullName).UseCollation("Vietnamese_CI_AI");
            e.HasIndex(x => x.CardNumber).IsUnique();
            e.HasIndex(x => x.AppUserId).IsUnique().HasFilter("[AppUserId] IS NOT NULL");
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.ReaderType).WithMany(t => t.Readers).HasForeignKey(x => x.ReaderTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AppUser).WithOne(u => u.Reader).HasForeignKey<Reader>(x => x.AppUserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Loan>(e =>
        {
            e.Property(x => x.Code).IsUnicode(false);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Reader).WithMany(r => r.Loans).HasForeignKey(x => x.ReaderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Librarian).WithMany().HasForeignKey(x => x.LibrarianId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(x => !x.Reader!.IsDeleted);
        });

        b.Entity<LoanItem>(e =>
        {
            e.HasIndex(x => new { x.CopyId, x.Status });
            e.HasIndex(x => new { x.Status, x.DueDate });
            e.HasOne(x => x.Loan).WithMany(l => l.Items).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Copy).WithMany(c => c.LoanItems).HasForeignKey(x => x.CopyId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.Loan!.Reader!.IsDeleted);
        });

        b.Entity<Reservation>(e =>
        {
            e.HasIndex(x => new { x.BookId, x.Status, x.CreatedAt });
            e.HasOne(x => x.Book).WithMany(bk => bk.Reservations).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reader).WithMany(r => r.Reservations).HasForeignKey(x => x.ReaderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Copy).WithMany().HasForeignKey(x => x.CopyId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.Book!.IsDeleted && !x.Reader!.IsDeleted);
        });

        b.Entity<Fine>(e =>
        {
            e.HasIndex(x => new { x.ReaderId, x.IsPaid });
            e.HasOne(x => x.Reader).WithMany(r => r.Fines).HasForeignKey(x => x.ReaderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.LoanItem).WithMany().HasForeignKey(x => x.LoanItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.Reader!.IsDeleted);
        });

        b.Entity<ScanLog>(e =>
        {
            e.Property(x => x.Code).IsUnicode(false);
            e.HasIndex(x => x.ScannedAt);
        });
    }
}
