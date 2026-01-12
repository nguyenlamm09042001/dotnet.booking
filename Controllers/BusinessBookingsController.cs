using booking.Data;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize(Roles = "business")]
public class BusinessBookingsController : Controller
{
    private const string V_INDEX = "~/Views/Business/Bookings/Index.cshtml";
    private const string V_DETAILS = "~/Views/Business/Bookings/Details.cshtml";

    private readonly AppDbContext _db;

    public BusinessBookingsController(AppDbContext db)
    {
        _db = db;
    }

    // =========================
    // Helpers
    // =========================
    private int? CurrentUserId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(s, out var id)) return id;
        return null;
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    private static bool IsValidStatus(string? s)
    {
        s = Norm(s);
        return s is "pending" or "confirmed" or "completed" or "canceled";
    }

    private IActionResult RedirectSafe(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    private void SetSwal(string type, string title, string message, string? redirect = null)
    {
        TempData["SwalType"] = type;       // success | error | warning | info | question
        TempData["SwalTitle"] = title;
        TempData["SwalMessage"] = message;
        if (!string.IsNullOrWhiteSpace(redirect))
            TempData["SwalRedirect"] = redirect;
    }

    /// <summary>
    /// Lấy BookingOrder theo id nhưng đảm bảo thuộc business đang đăng nhập
    /// (scope theo Services.UserId)
    /// </summary>
    private async Task<Models.BookingOrder?> FindScopedBookingOrder(int id)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return null;

        var bo = await (
            from x in _db.BookingOrders
            join s in _db.Services on x.ServiceId equals s.Id
            where x.Id == id && s.UserId == businessUserId.Value
            select x
        ).FirstOrDefaultAsync();

        return bo;
    }

    // =========================
    // GET: /BusinessBookings/Index?q=&status=&date=&page=
    // =========================
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status, string? date, int page = 1)
    {
        const int pageSize = 10;

        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        q = (q ?? "").Trim();
        status = (status ?? "").Trim();
        date = (date ?? "").Trim();

        // Parse date filter yyyy-MM-dd => DateOnly?
        DateOnly? dateFilter = null;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                dateFilter = d;
        }

        // Base query: BookingOrders join Services (scope business theo Services.UserId)
        var query =
            from bo in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on bo.ServiceId equals s.Id
            where s.UserId == businessUserId.Value
            select new { bo, s };

        // Filter q: customer / phone / service
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.bo.CustomerName.Contains(q) ||
                x.bo.Phone.Contains(q) ||
                x.s.Name.Contains(q));
        }

        // Filter status
        if (!string.IsNullOrWhiteSpace(status) && IsValidStatus(status))
        {
            var st = Norm(status);
            query = query.Where(x => x.bo.Status == st);
        }

        // Filter date
        if (dateFilter.HasValue)
        {
            var d = dateFilter.Value;
            query = query.Where(x => x.bo.Date == d);
        }

        // Sort
        query = query.OrderByDescending(x => x.bo.CreatedAt).ThenByDescending(x => x.bo.Id);

        // Pagination
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BusinessBookingRowVm
            {
                Id = x.bo.Id,
                CustomerName = x.bo.CustomerName,
                Phone = x.bo.Phone,
                ServiceName = x.s.Name,
                Note = x.bo.Note,
                Date = x.bo.Date,
                Time = x.bo.Time,
                Status = x.bo.Status
            })
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        return View(V_INDEX, items);
    }

    // =========================
    // GET: /BusinessBookings/Details/5?returnUrl=...
    // =========================
    [HttpGet]
    public async Task<IActionResult> Details(int id, string? returnUrl = null)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        // booking + service (scope theo business)
        var row = await (
            from bo in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on bo.ServiceId equals s.Id
            where bo.Id == id && s.UserId == businessUserId.Value
            select new { bo, s }
        ).FirstOrDefaultAsync();

        if (row == null) return NotFound();

        // review (optional) theo BookingOrderId
        var rv = await _db.ServiceReviews.AsNoTracking()
            .Where(x => x.BookingOrderId == id && x.ServiceId == row.s.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Stars, x.Rating, x.Comment, x.CreatedAt })
            .FirstOrDefaultAsync();

        var vm = new BusinessBookingDetailsVm
        {
            // booking
            Id = row.bo.Id,
            ServiceId = row.bo.ServiceId,
            UserId = row.bo.UserId,
            CustomerName = row.bo.CustomerName,
            Phone = row.bo.Phone,
            Date = row.bo.Date,
            Time = row.bo.Time,
            Note = row.bo.Note,
            Status = row.bo.Status,
            CreatedAt = row.bo.CreatedAt,

            // service
            ServiceName = row.s.Name,
            Category = row.s.Category,
            Description = row.s.Description,
            Price = row.s.Price,
            DurationMinutes = row.s.DurationMinutes,
            Location = row.s.Location,
            ServiceIsActive = row.s.IsActive,

            // review
            ReviewStars = rv?.Stars,
            ReviewRating = rv?.Rating,
            ReviewComment = rv?.Comment,
            ReviewCreatedAt = rv?.CreatedAt
        };

        ViewBag.ReturnUrl = returnUrl;

        return View(V_DETAILS, vm);
    }

    // =========================
    // POST: /BusinessBookings/Approve (pending -> confirmed)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl = null)
    {
        var bo = await FindScopedBookingOrder(id);
        if (bo == null) return NotFound();

        if (Norm(bo.Status) != "pending")
        {
            SetSwal("warning", "Không hợp lệ", "Chỉ có thể duyệt lịch đang ở trạng thái Chờ xác nhận.", returnUrl);
            return RedirectSafe(returnUrl);
        }

        bo.Status = "confirmed";
        await _db.SaveChangesAsync();

        SetSwal("success", "Đã duyệt", "Lịch hẹn đã được xác nhận.", returnUrl);
        return RedirectSafe(returnUrl);
    }

    // =========================
    // POST: /BusinessBookings/Complete (confirmed -> completed)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? returnUrl = null)
    {
        var bo = await FindScopedBookingOrder(id);
        if (bo == null) return NotFound();

        if (Norm(bo.Status) != "confirmed")
        {
            SetSwal("warning", "Không hợp lệ", "Chỉ có thể hoàn thành lịch đã được xác nhận.", returnUrl);
            return RedirectSafe(returnUrl);
        }

        bo.Status = "completed";
        await _db.SaveChangesAsync();

        SetSwal("success", "Hoàn thành", "Đã đánh dấu lịch hẹn là hoàn thành.", returnUrl);
        return RedirectSafe(returnUrl);
    }

    // =========================
    // POST: /BusinessBookings/Cancel (pending/confirmed -> canceled)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? returnUrl = null)
    {
        var bo = await FindScopedBookingOrder(id);
        if (bo == null) return NotFound();

        var st = Norm(bo.Status);

        if (st is "completed" or "canceled")
        {
            SetSwal("warning", "Không hợp lệ", "Lịch đã hoàn thành/đã hủy thì không thể hủy nữa.", returnUrl);
            return RedirectSafe(returnUrl);
        }

        bo.Status = "canceled";
        await _db.SaveChangesAsync();

        SetSwal("success", "Đã hủy", "Lịch hẹn đã được hủy.", returnUrl);
        return RedirectSafe(returnUrl);
    }
}
