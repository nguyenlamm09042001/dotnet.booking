namespace booking.ViewModels;

public class ReviewItemVm
{
    public int UserId { get; set; }
    public string? FullName { get; set; }

    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? Avatar { get; set; }
}
