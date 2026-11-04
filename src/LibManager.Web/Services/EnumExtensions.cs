// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W2-05 – entity, LibraryDbContext, dữ liệu mẫu (tuần 2, nhánh feature/tuan-csdl)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace LibManager.Web.Services;

public static class EnumExtensions
{
    public static string DisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    public static string FormatVnd(this decimal amount) => amount.ToString("#,##0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " đ";
}
