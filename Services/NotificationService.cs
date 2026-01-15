using booking.Data;
using booking.Models;

namespace booking.Services;

public class NotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    public async Task CreateAsync(int userId, string title, string message, string type = "info", string? linkUrl = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            LinkUrl = linkUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
