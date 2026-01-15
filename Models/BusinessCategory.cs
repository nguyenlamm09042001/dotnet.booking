using System.ComponentModel.DataAnnotations;

namespace booking.Models;

public class BusinessCategory
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = ""; // hair, car, hotel...

    [Required, MaxLength(120)]
    public string Name { get; set; } = ""; // Tóc, Xe, Khách sạn...

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<BusinessCategoryLink> BusinessLinks { get; set; } = new List<BusinessCategoryLink>();
}
