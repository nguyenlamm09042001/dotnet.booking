using booking.Data;
using booking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize(Roles = "business")]
public class BusinessReviewsController : Controller
{
    private const string V_INDEX = "~/Views/Business/Reviews/Index.cshtml";
    private readonly AppDbContext _db;

    public BusinessReviewsController(AppDbContext db)
    {
        _db = db;
    }

    // GET: /BusinessReviews/Index
    public async Task<IActionResult> Index()
    {
        // ===== businessUserId (Services.UserId) =====
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var businessUserId))
            return Unauthorized();

       var reviews = await _db.ServiceReviews
    .AsNoTracking()
    .Include(r => r.Service)
    .Include(r => r.User)
    .Where(r => r.Service.UserId == businessUserId)
    .OrderByDescending(r => r.CreatedAt)
    .ToListAsync();

        return View(V_INDEX, reviews);
    }
}
