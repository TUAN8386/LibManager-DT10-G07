// ============================================================
// Người thực hiện: Bùi Văn Long – MSSV 2410900047
// Công việc: W3-04 – mã bản sao Luhn, Code 128, in nhãn (tuần 3, nhánh feature/long-ban-sao)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

using System.Globalization;
using System.Text;

namespace LibManager.Web.Services;

/// <summary>
/// Sinh mã vạch Code 128 (bộ mã B) dạng SVG phía máy chủ – không cần thư viện ngoài,
/// in được cả khi máy không có Internet. Chuẩn: ISO/IEC 15417.
/// </summary>
public static class Code128
{
    // Độ rộng (bar, space, bar, space, bar, space) của 107 ký hiệu; 106 = Stop (7 phần tử).
    private static readonly string[] Patterns =
    {
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112",
    };

    private const int StartB = 104;
    private const int Stop = 106;

    /// <summary>Chuỗi giá trị ký hiệu (kể cả Start, checksum, Stop) cho văn bản ASCII 32..127.</summary>
    public static int[] Encode(string text)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("Nội dung rỗng.", nameof(text));
        var values = new List<int> { StartB };
        int checksum = StartB;
        for (int i = 0; i < text.Length; i++)
        {
            int ch = text[i];
            if (ch < 32 || ch > 127) throw new ArgumentException($"Ký tự không hỗ trợ: '{text[i]}'", nameof(text));
            int v = ch - 32;
            values.Add(v);
            checksum += v * (i + 1);
        }
        values.Add(checksum % 103);
        values.Add(Stop);
        return values.ToArray();
    }

    /// <summary>Chuỗi độ rộng mô-đun (luân phiên vạch/khoảng trắng, bắt đầu bằng vạch).</summary>
    public static string Widths(string text) => string.Concat(Encode(text).Select(v => Patterns[v]));

    /// <summary>Tạo SVG mã vạch. moduleWidth: độ rộng 1 mô-đun (px); height: chiều cao vạch.</summary>
    public static string ToSvg(string text, double moduleWidth = 1.6, double height = 46, bool showText = true, double fontSize = 11)
    {
        var widths = Widths(text);
        const int quiet = 10;
        int totalModules = widths.Sum(c => c - '0') + quiet * 2;
        double w = totalModules * moduleWidth;
        double h = height + (showText ? fontSize + 4 : 0);
        var inv = CultureInfo.InvariantCulture;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"barcode\" role=\"img\" aria-label=\"{text}\" ");
        sb.Append(string.Format(inv, "width=\"{0:0.##}\" height=\"{1:0.##}\" viewBox=\"0 0 {0:0.##} {1:0.##}\">", w, h));
        sb.Append(string.Format(inv, "<rect width=\"{0:0.##}\" height=\"{1:0.##}\" fill=\"#fff\"/>", w, h));
        double x = quiet * moduleWidth;
        bool bar = true;
        foreach (char c in widths)
        {
            double cw = (c - '0') * moduleWidth;
            if (bar) sb.Append(string.Format(inv, "<rect x=\"{0:0.###}\" y=\"0\" width=\"{1:0.###}\" height=\"{2:0.##}\" fill=\"#000\"/>", x, cw, height));
            x += cw;
            bar = !bar;
        }
        if (showText)
            sb.Append(string.Format(inv, "<text x=\"{0:0.##}\" y=\"{1:0.##}\" font-family=\"Consolas,monospace\" font-size=\"{2}\" text-anchor=\"middle\" letter-spacing=\"1\">{3}</text>",
                w / 2, height + fontSize + 1, fontSize, System.Net.WebUtility.HtmlEncode(text)));
        sb.Append("</svg>");
        return sb.ToString();
    }
}
