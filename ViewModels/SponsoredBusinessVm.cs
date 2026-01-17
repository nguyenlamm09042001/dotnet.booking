namespace booking.ViewModels;

public class SponsoredBusinessVm
{
    public int BusinessUserId { get; set; }
    public string FullName { get; set; } = "";
    public string? Avatar { get; set; }
    public double AvgRating { get; set; }
    public int TotalReviews { get; set; }
    public int ServiceCount { get; set; }
}
