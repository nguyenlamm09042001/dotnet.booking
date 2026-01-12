using booking.Data;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize(Roles = "business")]
public class BusinessController : Controller
{
    private readonly AppDbContext _db;

    public BusinessController(AppDbContext db)
    {
        _db = db;
    }

    // GET: /Business/Index?q=&status=
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status)
    {
        var now = DateTime.Now;

        // ===== Query filter (dashboard recent bookings)
        q = (q ?? "").Trim();
        status = (status ?? "").Trim(); // pending|confirmed|completed|canceled
        var statusNorm = (status ?? "").Trim().ToLower();

        // ===== Business scope (RẤT QUAN TRỌNG)
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var businessUserId) || businessUserId <= 0)
            return Forbid(); // hoặc Unauthorized()

        ViewBag.BizId = businessUserId;
        ViewBag.BizIdStr = userIdStr;

        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // =========================
        // KPI: ServicesActive (đÃ SCOPE)
        // =========================
        var servicesActive = await _db.Services
            .AsNoTracking()
            .Where(s => s.IsActive && s.UserId == businessUserId)
            .CountAsync();

        // =========================
        // KPI: BookingsToday (đÃ SCOPE)
        // =========================
        var today = DateOnly.FromDateTime(now);

        var bookingsToday = await (
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where b.Date == today
               && s.UserId == businessUserId
            select b.Id
        ).CountAsync();

        // =========================
        // KPI: NewReviews7d (đÃ SCOPE)
        // =========================
        var from7d = now.AddDays(-7);

        var newReviews7d = await (
            from r in _db.ServiceReviews.AsNoTracking()
            join b in _db.BookingOrders.AsNoTracking() on r.BookingOrderId equals b.Id
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where r.CreatedAt >= from7d
               && s.UserId == businessUserId
            select r.Id
        ).CountAsync();

        // =========================
        // KPI: AvgRating (đÃ SCOPE)
        // =========================
        var avgRating = await (
            from r in _db.ServiceReviews.AsNoTracking()
            join b in _db.BookingOrders.AsNoTracking() on r.BookingOrderId equals b.Id
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where s.UserId == businessUserId
            select (double?)r.Rating
        ).AverageAsync() ?? 0.0;

        // =========================
        // KPI: RevenueMonth (đÃ SCOPE)
        // =========================
        var revenueMonthDecimal = await (
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where b.CreatedAt >= monthStart
               && b.CreatedAt < monthEnd
               && (b.Status ?? "").Trim().ToLower() == "completed"
               && s.UserId == businessUserId
            select (decimal?)s.Price
        ).SumAsync() ?? 0m;

        var revenueMonth = (int)Math.Round(revenueMonthDecimal, 0);

        // =========================
        // KPI: CancelRate (đÃ SCOPE)
        // =========================
        var monthTotal = await (
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where b.CreatedAt >= monthStart
               && b.CreatedAt < monthEnd
               && s.UserId == businessUserId
            select b.Id
        ).CountAsync();

        var monthCanceled = await (
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where b.CreatedAt >= monthStart
               && b.CreatedAt < monthEnd
               && (b.Status ?? "").Trim().ToLower() == "canceled"
               && s.UserId == businessUserId
            select b.Id
        ).CountAsync();

        var cancelRate = monthTotal == 0 ? 0 : (int)Math.Round(monthCanceled * 100.0 / monthTotal, 0);

        // =========================
        // RecentBookings (TOP 5) + FILTER q/status (đÃ SCOPE)
        // =========================
        var recentQuery =
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where s.UserId == businessUserId
            select new
            {
                b.Id,
                b.CustomerName,
                b.Phone,
                b.Date,
                b.Time,
                b.Status,
                ServiceName = s.Name
            };

        if (!string.IsNullOrWhiteSpace(statusNorm))
        {
            recentQuery = recentQuery.Where(x =>
                (x.Status ?? "").Trim().ToLower() == statusNorm
            );
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            recentQuery = recentQuery.Where(x =>
                (x.CustomerName ?? "").Contains(q) ||
                (x.Phone ?? "").Contains(q) ||
                (x.ServiceName ?? "").Contains(q)
            );
        }

        var recentBookings = await recentQuery
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Time)
            .Take(5)
            .Select(x => new BusinessDashboardVm.RecentBookingRow
            {
                Id = x.Id,
                CustomerName = x.CustomerName,
                Phone = x.Phone,
                ServiceName = x.ServiceName,
                Date = x.Date,
                Time = x.Time,
                Status = x.Status
            })
            .ToListAsync();

        // =========================
        // RecentReviews (top 3) (đÃ SCOPE)
        // =========================
        var recentReviews = await (
            from r in _db.ServiceReviews.AsNoTracking()
            join b in _db.BookingOrders.AsNoTracking() on r.BookingOrderId equals b.Id
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where s.UserId == businessUserId
            orderby r.CreatedAt descending
            select new BusinessDashboardVm.RecentReviewRow
            {
                Id = r.Id,
                CustomerName = b.CustomerName,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            }
        ).Take(3).ToListAsync();

        // =========================
        // PendingBookings (đã đúng sẵn)
        // =========================
        var pendingBookings = await (
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where s.UserId == businessUserId
               && (b.Status ?? "").Trim().ToLower() == "pending"
            select b.Id
        ).CountAsync();

        var pendingSwapRequests = 0;
        var pendingReviewsToHandle = 0;

        var vm = new BusinessDashboardVm
        {
            RevenueMonth = revenueMonth,
            ServicesActive = servicesActive,
            BookingsToday = bookingsToday,
            NewReviews7d = newReviews7d,
            CancelRate = cancelRate,
            AvgRating = avgRating,
            RecentBookings = recentBookings,
            RecentReviews = recentReviews,
            PendingBookings = pendingBookings,
            PendingSwapRequests = pendingSwapRequests,
            PendingReviews = pendingReviewsToHandle
        };

        return View("Home/Index", vm);
    }
}
