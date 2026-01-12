using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace booking.Models;

public class StaffProfile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // ✅ KHÔNG identity
    public int UserId { get; set; }                   // ✅ PK = FK to Users.Id

    public int BusinessUserId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; } // optional navigation
}
