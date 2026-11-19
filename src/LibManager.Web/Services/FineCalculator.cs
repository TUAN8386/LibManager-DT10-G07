// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W4-01/W4-03 – CirculationService mượn – trả, FineCalculator (tuần 4, nhánh feature/tuan-luu-thong)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

namespace LibManager.Web.Services;

/// <summary>Quy tắc tính hạn trả và phí phạt (BR-03, BR-05, BR-06). Lớp thuần, dễ unit test.</summary>
public static class FineCalculator
{
    /// <summary>Số ngày quá hạn tính theo ngày lịch (trả trong ngày đến hạn = không quá hạn).</summary>
    public static int OverdueDays(DateTime dueDate, DateTime at)
        => Math.Max(0, (at.Date - dueDate.Date).Days);

    /// <summary>Phí quá hạn = số ngày × mức phí/ngày, không vượt giá bìa (nếu giá bìa &gt; 0).</summary>
    public static decimal OverdueFine(int overdueDays, decimal finePerDay, decimal bookPrice)
    {
        if (overdueDays <= 0 || finePerDay <= 0) return 0;
        var fine = overdueDays * finePerDay;
        return bookPrice > 0 ? Math.Min(fine, bookPrice) : fine;
    }

    /// <summary>Bồi thường mất sách = giá bìa + phí xử lý.</summary>
    public static decimal LostCompensation(decimal bookPrice, decimal processingFee)
        => Math.Max(0, bookPrice) + Math.Max(0, processingFee);

    /// <summary>Hạn trả: cuối ngày (23:59:59) của ngày mượn + số ngày mượn.</summary>
    public static DateTime DueDate(DateTime loanDate, int loanDays)
        => loanDate.Date.AddDays(loanDays).AddDays(1).AddTicks(-1);

    /// <summary>Gia hạn: cộng thêm số ngày mượn vào hạn hiện tại.</summary>
    public static DateTime RenewedDueDate(DateTime currentDue, int loanDays)
        => currentDue.Date.AddDays(loanDays).AddDays(1).AddTicks(-1);
}
