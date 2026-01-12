namespace booking.ViewModels;

public class StaffDetailBookingVm
{
    public int Id { get; set; }
    public string? Status { get; set; }

    public string? CustomerName { get; set; }
    public string? Phone { get; set; }
    public string? Note { get; set; }

    public int ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public string? Category { get; set; }
    public string? Location { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
}
