// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W3-01 – layout Bootstrap, partial dùng chung, CSS (tuần 3, nhánh feature/long-layout)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using LibManager.Web.Models.Entities;
using Microsoft.AspNetCore.Html;

namespace LibManager.Web.Services;

/// <summary>Helper hiển thị dùng trong Razor view.</summary>
public static class Ui
{
    public static IHtmlContent StatusBadge(CopyStatus s)
    {
        var cls = s switch
        {
            CopyStatus.Available => "text-bg-success",
            CopyStatus.OnLoan => "text-bg-primary",
            CopyStatus.OnHold => "text-bg-warning",
            CopyStatus.Damaged => "text-bg-danger",
            CopyStatus.Lost => "text-bg-dark",
            _ => "text-bg-secondary",
        };
        return new HtmlString($"<span class=\"badge {cls}\">{s.DisplayName()}</span>");
    }

    public static IHtmlContent ReservationBadge(ReservationStatus s)
    {
        var cls = s switch
        {
            ReservationStatus.Waiting => "text-bg-info",
            ReservationStatus.Ready => "text-bg-warning",
            ReservationStatus.Fulfilled => "text-bg-success",
            _ => "text-bg-secondary",
        };
        return new HtmlString($"<span class=\"badge {cls}\">{s.DisplayName()}</span>");
    }

    public static IHtmlContent DueBadge(DateTime due, DateTime now)
    {
        var days = FineCalculator.OverdueDays(due, now);
        if (days > 0) return new HtmlString($"<span class=\"badge text-bg-danger\">Quá hạn {days} ngày</span>");
        var left = (due.Date - now.Date).Days;
        if (left <= 2) return new HtmlString($"<span class=\"badge text-bg-warning\">Còn {left} ngày</span>");
        return new HtmlString($"<span class=\"badge text-bg-light border\">Còn {left} ngày</span>");
    }

    public static IHtmlContent Barcode(string code, double module = 1.6, double height = 46) => new HtmlString(Code128.ToSvg(code, module, height));

    public static string Vnd(decimal v) => v.FormatVnd();

    /// <summary>Ảnh bìa: dùng URL nếu có, nếu không vẽ bìa giả bằng màu theo nhan đề.</summary>
    public static IHtmlContent Cover(string title, string? url, string css = "cover")
    {
        if (!string.IsNullOrWhiteSpace(url))
            return new HtmlString($"<img src=\"{System.Net.WebUtility.HtmlEncode(url)}\" alt=\"\" class=\"{css}\" loading=\"lazy\" />");
        int hue = (title.Aggregate(17, (h, c) => unchecked(h * 31 + c)) & 0x7fffffff) % 360;
        var initials = string.Concat(title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
        return new HtmlString($"<div class=\"{css} cover-fallback\" style=\"--h:{hue}\"><span>{System.Net.WebUtility.HtmlEncode(initials)}</span></div>");
    }
}
