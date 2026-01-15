using booking.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) => _db = db;

    // GET: /Notifications
    public async Task<IActionResult> Index()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out var userId);

        var items = await _db.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();

        return View(items); // Views/Notifications/Index.cshtml
    }

    // POST: /Notifications/MarkRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out var userId);

        var noti = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (noti != null)
        {
            noti.IsRead = true;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
