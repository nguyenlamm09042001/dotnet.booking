namespace booking.ViewModels;

public class AdminMarketingDetailsVm
{
    public int Id { get; set; }
    public string? Code { get; set; }

    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = "—";
    public string BusinessEmail { get; set; } = "";

    public decimal Amount { get; set; }
    public int Days { get; set; }

    public DateTime? StartAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = "pending";
}
