using System.ComponentModel.DataAnnotations;

namespace booking.Models;

public class ServiceReview
{
    public int Id { get; set; }

    [Required]
    public int BookingOrderId { get; set; }
    public BookingOrder? BookingOrder { get; set; }

    [Required]
    public int ServiceId { get; set; }
    public Service? Service { get; set; }

    [Required]
    public int UserId { get; set; }
    public User? User { get; set; }

    [Range(1, 5)]
    public int Stars { get; set; }

    public int Rating { get; set; }  // <-- thêm dòng này (1..5)


    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
