namespace booking.ViewModels;

public class BusinessCardVm
{
    public int BusinessUserId { get; set; }
    public string FullName { get; set; } = "";
    public string? Avatar { get; set; }
    public string Status { get; set; } = "pending";

    public int ServiceCount { get; set; }
    public decimal AvgRating { get; set; }
    public int TotalReviews { get; set; }

    public List<string> Categories { get; set; } = new();
}
