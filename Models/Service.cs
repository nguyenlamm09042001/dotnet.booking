namespace booking.Models;

public class Service
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    
    public int UserId { get; set; }

    // Giá theo VND (để format đẹp)
    public decimal Price { get; set; }

    // Thời lượng phút
    public int DurationMinutes { get; set; }

    // Demo rating/reviews (sau nối bảng Review)
    public decimal Rating { get; set; } = 4.8m;
    public int ReviewCount { get; set; } = 120;

    // Optional
    public string Location { get; set; } = "TP.HCM";
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    
}
