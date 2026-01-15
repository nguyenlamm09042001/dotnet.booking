namespace booking.Models;

public class ServiceCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = "";   // unique: hair, spa, nails...
    public string Name { get; set; } = "";   // hiển thị

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Service> Services { get; set; } = new();
}
