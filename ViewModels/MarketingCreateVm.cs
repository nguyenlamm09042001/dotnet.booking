using System.ComponentModel.DataAnnotations;

namespace booking.ViewModels;

public class MarketingCreateVm
{
    [Range(1, 365)]
    public int Days { get; set; } = 1;

    [Required]
    public DateTime StartAt { get; set; } = DateTime.Today;

    public decimal UnitPrice { get; set; } = 15000m;
    public decimal Amount => Days * UnitPrice;
}
