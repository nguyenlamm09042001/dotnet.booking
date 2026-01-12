namespace booking.ViewModels;

public class BusinessBookingRowVm
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";

    public DateOnly Date { get; set; }      // ✅ đổi
    public TimeOnly Time { get; set; }      // ✅ đổi

    public string? Note { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }

    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = "";
}
