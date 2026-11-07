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

/// <summary>Quản lý thể loại.</summary>
[Authorize(Roles = Roles.Staff)]
public class CategoriesController : Controller
{
    private readonly LibraryDbContext _db;
    public CategoriesController(LibraryDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var list = await _db.Categories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new { Category = c, Count = c.Books.Count }).ToListAsync();
        ViewBag.Counts = list.ToDictionary(x => x.Category.Id, x => x.Count);
        return View(list.Select(x => x.Category).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Save(int id, string name, bool isActive = true)
    {
        name = (name ?? "").Trim();
        if (name.Length is 0 or > 100) { TempData["err"] = "Tên thể loại từ 1–100 ký tự."; return RedirectToAction(nameof(Index)); }
        var slug = Slugify(name);
        if (await _db.Categories.AnyAsync(c => c.Slug == slug && c.Id != id)) { TempData["err"] = "Thể loại đã tồn tại."; return RedirectToAction(nameof(Index)); }
        var cat = id == 0 ? new Category() : await _db.Categories.FindAsync(id);
        if (cat is null) return NotFound();
        cat.Name = name;
        cat.Slug = slug;
        cat.IsActive = isActive;
        if (id == 0) _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        TempData["ok"] = "Đã lưu thể loại.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat is null) return NotFound();
        if (await _db.Books.IgnoreQueryFilters().AnyAsync(b => b.CategoryId == id))
        {
            cat.IsActive = false;
            TempData["ok"] = "Thể loại đang có sách – đã chuyển sang ngừng sử dụng.";
        }
        else
        {
            _db.Categories.Remove(cat);
            TempData["ok"] = "Đã xóa thể loại.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    internal static string Slugify(string s)
    {
        var normalized = s.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
