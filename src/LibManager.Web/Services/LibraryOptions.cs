// ============================================================
// Người thực hiện: Đinh Anh Tuấn – MSSV 2410900083
// Công việc: W4-01/W4-03 – CirculationService mượn – trả, FineCalculator (tuần 4, nhánh feature/tuan-luu-thong)
// Đề tài DT10 LibManager – Nhóm 07 – K24CNT1
// ============================================================

namespace LibManager.Web.Services;

/// <summary>Cấu hình nghiệp vụ thư viện (appsettings.json, mục "Library").</summary>
public class LibraryOptions
{
    public const string Section = "Library";

    /// <summary>Tổng phí chưa nộp tối đa vẫn được mượn (BR-02).</summary>
    public decimal MaxUnpaidFine { get; set; } = 50_000;
    /// <summary>Số ngày giữ sách cho người đặt giữ khi sách được trả (BR-07).</summary>
    public int HoldDays { get; set; } = 3;
    /// <summary>Phí xử lý cộng thêm khi mất sách (BR-06).</summary>
    public decimal LostProcessingFee { get; set; } = 20_000;
    /// <summary>Gửi email nhắc trước hạn trả bao nhiêu ngày.</summary>
    public int ReminderDaysBefore { get; set; } = 2;
    /// <summary>Chu kỳ chạy tác vụ nền (phút).</summary>
    public int BackgroundIntervalMinutes { get; set; } = 60;
    /// <summary>Ngưỡng nhận diện máy quét phía trình duyệt (ms giữa 2 phím).</summary>
    public int ScanMaxInterKeyMs { get; set; } = 50;
    /// <summary>Độ dài tối thiểu của một lượt quét.</summary>
    public int ScanMinLength { get; set; } = 6;
    /// <summary>Ghi nhật ký quét (FR-7.5).</summary>
    public bool EnableScanLog { get; set; } = true;
}

public class SmtpOptions
{
    public const string Section = "Smtp";
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string FromName { get; set; } = "Thư viện LibManager";
    public string FromAddress { get; set; } = "no-reply@libmanager.local";
}
