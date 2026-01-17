namespace booking.ViewModels;

public class MarketingPayVm
{
    public int OrderId { get; set; }
    public int Id { get; set; }
    public string? PaymentNote { get; set; }

    public string PayCode { get; set; } = "";
    public decimal Amount { get; set; }
    public int Days { get; set; }
    public string StartAt { get; set; } = "—";

    public string QrUrl { get; set; } = "";
    public string TransferContent { get; set; } = "";
}
