using System.ComponentModel.DataAnnotations;

namespace booking.ViewModels;

public class BookingCreateVm
{
    [Required]
    public int ServiceId { get; set; }

    // hiển thị
    public string ServiceName { get; set; } = "";
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }

    // form input
    [Required, StringLength(120)]
    public string CustomerName { get; set; } = "";

    [Required, StringLength(20)]
    public string Phone { get; set; } = "";

    [Required]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required]
    public string Time { get; set; } = "09:00";

    [StringLength(500)]
    public string? Note { get; set; }

    // dropdown times (giữ lại để tương thích code cũ)
    public List<string> TimeOptions { get; set; } = new();

    // ✅ grid slot: mỗi giờ có trạng thái "đã đặt hay chưa"
    public List<TimeSlotVm> TimeSlots { get; set; } = new();
}

// ✅ VM cho từng khung giờ
public class TimeSlotVm
{
    public string Value { get; set; } = ""; // "09:00"
    public bool IsBooked { get; set; }      // true = đã có người đặt (không chọn)
}
