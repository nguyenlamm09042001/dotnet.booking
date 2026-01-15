using System.ComponentModel.DataAnnotations;

namespace booking.Models;

public class BookingOrder
{
    public int Id { get; set; }

    // ✅ Gắn đơn với user đang đăng nhập
    [Required]
    public int UserId { get; set; }
    public User? User { get; set; }

    [Required]
    public int ServiceId { get; set; }
    public Service? Service { get; set; }

    [Required, StringLength(120)]
    public string CustomerName { get; set; } = "";

    [Required, StringLength(20)]
    public string Phone { get; set; } = "";

    [Required]
    public DateOnly Date { get; set; }


    [Required]
    public TimeOnly Time { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? StaffUserId { get; set; }

public DateTime? UpdatedAt { get; set; }


}
