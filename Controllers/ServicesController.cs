using booking.Data;
using booking.Models;
using booking.ViewModels; // ✅ thêm
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

public class ServicesController : Controller
{
    private readonly AppDbContext _db;

    public ServicesController(AppDbContext db)
    {
        _db = db;
    }

    // /Services 또는 Home gọi sang view này cũng được
    [HttpGet]
    public async Task<IActionResult> Index(
        string? q,
        string? location,
        string? category,
        string? sort
    )
    {
        var query = _db.Services.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var k = q.Trim();
            query = query.Where(s =>
                s.Name.Contains(k) ||
                (s.Description != null && s.Description.Contains(k)) ||
                (s.Location != null && s.Location.Contains(k)) ||
                (s.Category != null && s.Category.Contains(k))
            );
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim();
            query = query.Where(s => s.Location != null && s.Location.Contains(loc));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            query = query.Where(s => s.Category != null && s.Category == cat);
        }

        query = sort switch
        {
            "rating" => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount),
            "price_asc" => query.OrderBy(s => s.Price),
            "price_desc" => query.OrderByDescending(s => s.Price),
            "new" => query.OrderByDescending(s => s.Id),
            _ => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount)
        };

        var services = await query.ToListAsync();

        ViewBag.Q = q ?? "";
        ViewBag.Location = location ?? "";
        ViewBag.Category = category ?? "";
        ViewBag.Sort = sort ?? "";

        return View(services);
    }

    // /Services/Details/2
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (service == null) return NotFound();

        // ✅ JOIN Users để lấy FullName
        var reviews = await (
            from r in _db.ServiceReviews.AsNoTracking()
            join u in _db.Users.AsNoTracking() on r.UserId equals u.Id into uu
            from u in uu.DefaultIfEmpty()
            where r.ServiceId == id
            orderby r.CreatedAt descending
            select new ReviewItemVm
            {
                UserId = r.UserId,
                FullName = u != null ? u.FullName : null,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            }
        ).ToListAsync();

        var count = reviews.Count;
        var avg = count == 0 ? 0 : reviews.Average(x => x.Rating);

        var vm = new ServiceDetailsVm
        {
            Service = service,
            Reviews = reviews,
            AvgRating = avg,
            ReviewCount = count
        };

        return View("~/Views/User/Services/Details.cshtml", vm);
    }
}
