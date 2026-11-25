// ============================================================
// Người thực hiện: Lê Huy Hoàng – MSSV 2410900038
// Công việc: W5-03 – màn hình React tra cứu bản sao tức thời (tuần 5, nhánh feature/hoang-tra-cuu-react)
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

/// <summary>Màn hình tra cứu bản sao tức thời (React).</summary>
[Authorize(Roles = Roles.Staff)]
public class LookupController : Controller
{
    private readonly LibraryOptions _opt;
    public LookupController(IOptions<LibraryOptions> opt) => _opt = opt.Value;

    public IActionResult Index(string? code)
    {
        ViewBag.Code = code;
        ViewBag.ScanMaxInterKeyMs = _opt.ScanMaxInterKeyMs;
        ViewBag.ScanMinLength = _opt.ScanMinLength;
        return View();
    }
}
