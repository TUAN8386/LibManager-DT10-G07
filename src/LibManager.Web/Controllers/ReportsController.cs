// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-04 – dashboard lưu thông bằng React (tuần 5, nhánh feature/hoang-dashboard)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.Security.Claims;
using LibManager.Web.Data;
using LibManager.Web.Models.Entities;
using LibManager.Web.Services;
using LibManager.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibManager.Web.Controllers;

/// <summary>Báo cáo và thống kê (FR-5.x).</summary>
[Authorize(Roles = Roles.Staff)]
public class ReportsController : Controller
{
    private readonly IReportService _reports;
    private readonly TimeProvider _time;

    public ReportsController(IReportService reports, TimeProvider time)
    {
        _reports = reports;
        _time = time;
    }

    /// <summary>Dashboard lưu thông (React).</summary>
    [Authorize(Roles = Roles.Admin)]
    public IActionResult Index() => View();

    /// <summary>Bảng sách quá hạn (React).</summary>
    public IActionResult Overdue() => View();

    /// <summary>Xuất báo cáo Excel.</summary>
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Export(DateTime? from, DateTime? to)
    {
        var t = (to ?? _time.GetLocalNow().DateTime).Date;
        var f = (from ?? t.AddDays(-29)).Date;
        if (f > t) (f, t) = (t, f);
        var bytes = await _reports.ExportExcelAsync(f, t);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCaoThuVien_{f:yyyyMMdd}_{t:yyyyMMdd}.xlsx");
    }
}
