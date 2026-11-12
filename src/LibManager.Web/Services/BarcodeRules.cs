// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W3-04 – mã bản sao Luhn, Code 128, in nhãn (tuần 3, nhánh feature/long-ban-sao)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.Text.RegularExpressions;

namespace LibManager.Web.Services;

public enum CodeType
{
    Unknown = 0,
    ReaderCard = 1,
    Copy = 2,
    Isbn = 3,
    Command = 4,
}

/// <summary>Kết quả phân loại một chuỗi mã quét được.</summary>
public record CodeInfo(string Raw, string Normalized, CodeType Type, bool IsValid, string? Error)
{
    public string TypeName => Type switch
    {
        CodeType.ReaderCard => "card",
        CodeType.Copy => "copy",
        CodeType.Isbn => "isbn",
        CodeType.Command => "command",
        _ => "unknown",
    };
}

/// <summary>
/// Quy ước mã vạch của LibManager (xem tài liệu DT10-04, mục 6):
///  - Bản sao  : BS + 9 chữ số tuần tự + 1 chữ số kiểm tra Luhn   (vd. BS0000000018)
///  - Thẻ độc giả: DG + 2 số năm cấp + 6 số tuần tự               (vd. DG24000001)
///  - ISBN-13  : 978/979 + 10 chữ số, chữ số cuối là check digit EAN-13
///  - Mã lệnh  : CMD-CONFIRM, CMD-CANCEL (in sẵn tại quầy)
/// </summary>
public static partial class BarcodeRules
{
    public const string CopyPrefix = "BS";
    public const string CardPrefix = "DG";

    [GeneratedRegex(@"^BS\d{10}$")] private static partial Regex CopyRegex();
    [GeneratedRegex(@"^DG\d{8}$")] private static partial Regex CardRegex();
    [GeneratedRegex(@"^97[89]\d{10}$")] private static partial Regex IsbnRegex();
    [GeneratedRegex(@"^CMD-[A-Z]+$")] private static partial Regex CommandRegex();

    /// <summary>Chuẩn hóa: bỏ khoảng trắng, ký tự điều khiển, gạch nối; viết hoa.</summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = new string(raw.Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)).ToArray()).ToUpperInvariant();
        // ISBN thường in kèm dấu gạch: 978-604-1-12345-6
        if (s.Length > 0 && char.IsDigit(s[0])) s = s.Replace("-", "");
        return s;
    }

    public static CodeInfo Classify(string? raw)
    {
        var code = Normalize(raw);
        if (code.Length == 0) return new CodeInfo(raw ?? "", code, CodeType.Unknown, false, "Mã rỗng.");

        if (CopyRegex().IsMatch(code))
            return IsValidLuhn(code[2..])
                ? new CodeInfo(raw!, code, CodeType.Copy, true, null)
                : new CodeInfo(raw!, code, CodeType.Copy, false, "Mã bản sao sai chữ số kiểm tra (có thể quét/nhập nhầm).");

        if (CardRegex().IsMatch(code))
            return new CodeInfo(raw!, code, CodeType.ReaderCard, true, null);

        if (IsbnRegex().IsMatch(code))
            return IsValidIsbn13(code)
                ? new CodeInfo(raw!, code, CodeType.Isbn, true, null)
                : new CodeInfo(raw!, code, CodeType.Isbn, false, "ISBN-13 sai chữ số kiểm tra.");

        if (CommandRegex().IsMatch(code))
            return new CodeInfo(raw!, code, CodeType.Command, true, null);

        return new CodeInfo(raw!, code, CodeType.Unknown, false, "Không nhận dạng được loại mã.");
    }

    /// <summary>Tính chữ số kiểm tra Luhn cho chuỗi chữ số (chưa có check digit).</summary>
    public static int LuhnCheckDigit(string payload)
    {
        if (payload.Length == 0 || !payload.All(char.IsDigit))
            throw new ArgumentException("Payload phải là chuỗi chữ số.", nameof(payload));
        int sum = 0;
        bool dbl = true; // chữ số ngoài cùng bên phải của payload sẽ được nhân đôi
        for (int i = payload.Length - 1; i >= 0; i--)
        {
            int d = payload[i] - '0';
            if (dbl) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            dbl = !dbl;
        }
        return (10 - sum % 10) % 10;
    }

    /// <summary>Kiểm tra chuỗi chữ số có chữ số cuối là check digit Luhn hợp lệ.</summary>
    public static bool IsValidLuhn(string digitsWithCheck)
    {
        if (digitsWithCheck.Length < 2 || !digitsWithCheck.All(char.IsDigit)) return false;
        var payload = digitsWithCheck[..^1];
        return LuhnCheckDigit(payload) == digitsWithCheck[^1] - '0';
    }

    public static bool IsValidCopyBarcode(string? code)
    {
        var c = Normalize(code);
        return CopyRegex().IsMatch(c) && IsValidLuhn(c[2..]);
    }

    /// <summary>Sinh mã vạch bản sao từ số tuần tự (1 … 999.999.999).</summary>
    public static string MakeCopyBarcode(long sequence)
    {
        if (sequence < 1 || sequence > 999_999_999) throw new ArgumentOutOfRangeException(nameof(sequence));
        var payload = sequence.ToString("D9");
        return CopyPrefix + payload + LuhnCheckDigit(payload);
    }

    /// <summary>Lấy số tuần tự từ mã bản sao hợp lệ (dùng để sinh mã kế tiếp).</summary>
    public static long CopySequence(string barcode) => long.Parse(barcode.Substring(2, 9));

    /// <summary>Sinh mã thẻ độc giả: DG + yy + 6 số tuần tự.</summary>
    public static string MakeCardNumber(int year, int sequence)
    {
        if (sequence < 1 || sequence > 999_999) throw new ArgumentOutOfRangeException(nameof(sequence));
        return CardPrefix + (year % 100).ToString("D2") + sequence.ToString("D6");
    }

    public static bool IsValidIsbn13(string? isbn)
    {
        var s = Normalize(isbn);
        if (s.Length != 13 || !s.All(char.IsDigit)) return false;
        int sum = 0;
        for (int i = 0; i < 12; i++) sum += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);
        int check = (10 - sum % 10) % 10;
        return check == s[12] - '0';
    }
}
