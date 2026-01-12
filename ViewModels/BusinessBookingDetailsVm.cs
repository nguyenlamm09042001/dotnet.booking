using System;

namespace booking.ViewModels;

public class BusinessBookingDetailsVm
{
    // BookingOrders
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public int UserId { get; set; } // customer user id
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }

    // Services
    public string ServiceName { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public bool ServiceIsActive { get; set; }

    // ServiceReviews (optional)
    public int? ReviewStars { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? ReviewCreatedAt { get; set; }
}
