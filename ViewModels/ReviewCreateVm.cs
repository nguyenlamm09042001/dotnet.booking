using System.ComponentModel.DataAnnotations;

namespace booking.ViewModels;

public class ReviewCreateVm
{
    public int BookingOrderId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public int UserId { get; set; }
    public string? FullName { get; set; }
}
