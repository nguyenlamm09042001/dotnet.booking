namespace booking.ViewModels;

public class BookingIndexItemVm
{
    public int Id { get; set; }

    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = "Dịch vụ";

    public string Status { get; set; } = "Pending";

    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";

    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    // ✅ flags review cho trang Index
    public bool IsTimePassed { get; set; }
    public bool IsCompletedLogic { get; set; }
    public bool HasReview { get; set; }
}
