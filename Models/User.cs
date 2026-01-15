using System.ComponentModel.DataAnnotations;

namespace booking.Models;

public class User
{
    public int Id { get; set; }

    public string? FullName { get; set; }


    [Required, MaxLength(255)]
    public string Email { get; set; } = "";

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Role { get; set; } = "customer";

    public string Status { get; set; } = "active";

    public DateTime? BusinessApprovedAt { get; set; }
    public int? BusinessApprovedBy { get; set; }
    public int BusinessRiskLevel { get; set; } = 0;
    public DateTime? BusinessVerifiedAt { get; set; }

    public string? Avatar { get; set; }

}
