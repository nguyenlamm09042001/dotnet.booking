namespace booking.Models;

public class Notification
{
    public int Id { get; set; }

    // người nhận
    public int UserId { get; set; }

    public string Title { get; set; } = "";
    public string Message { get; set; } = "";

    // info | success | warning | error
    public string Type { get; set; } = "info";

    public bool IsRead { get; set; } = false;

    public string? LinkUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
