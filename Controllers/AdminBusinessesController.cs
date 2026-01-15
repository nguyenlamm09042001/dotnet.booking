using booking.Data;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize(Roles = "admin")]
public class AdminBusinessesController : Controller
{
    private readonly AppDbContext _db;
    public AdminBusinessesController(AppDbContext db) => _db = db;

    private void SetSwal(string type, string title, string message, string? redirect = null)
    {
        TempData["SwalType"] = type;
        TempData["SwalTitle"] = title;
        TempData["SwalMessage"] = message;
        if (!string.IsNullOrWhiteSpace(redirect)) TempData["SwalRedirect"] = redirect;
    }

    private int? CurrentAdminId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(s, out var id) ? id : null;
    }

    private IActionResult RedirectSafe(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    // =========================
    // GET: /AdminBusinesses/Index?q=&status=&sort=&page=&pageSize=
    // status: all|pending|active|suspended|rejected
    // sort: newest|oldest|risk_desc|risk_asc|name|email
    // =========================
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status, string? sort, int page = 1, int pageSize = 12)
    {
        // ===== normalize input =====
        q = (q ?? "").Trim();
        status = (status ?? "all").Trim().ToLowerInvariant();
        sort = (sort ?? "newest").Trim().ToLowerInvariant();

        var allowedStatus = new[] { "all", "pending", "active", "suspended", "rejected" };
        if (!allowedStatus.Contains(status)) status = "all";

        var allowedSort = new[] { "newest", "oldest", "risk_desc", "risk_asc", "name", "email" };
        if (!allowedSort.Contains(sort)) sort = "newest";

        pageSize = pageSize switch
        {
            10 => 10,
            20 => 20,
            50 => 50,
            _ => 12
        };

        // ✅ tổng tất cả doanh nghiệp (không filter)
        var totalBusinesses = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business");

        // ===== base query (role business) =====
        var query = _db.Users.AsNoTracking()
            .Where(u => u.Role == "business")
            .AsQueryable();

        // ===== filter status =====
        if (status != "all")
            query = query.Where(u => (u.Status ?? "pending") == status);

        // ===== search =====
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(u =>
                (u.FullName ?? "").Contains(q) ||
                (u.Email ?? "").Contains(q));
        }

        // ===== sorting =====
        query = sort switch
        {
            "oldest" => query.OrderBy(u => u.CreatedAt),
            "risk_desc" => query.OrderByDescending(u => u.BusinessRiskLevel).ThenByDescending(u => u.CreatedAt),
            "risk_asc" => query.OrderBy(u => u.BusinessRiskLevel).ThenByDescending(u => u.CreatedAt),
            "name" => query.OrderBy(u => u.FullName),
            "email" => query.OrderBy(u => u.Email),
            _ => query.OrderByDescending(u => u.CreatedAt) // newest
        };

        // ✅ total theo filter hiện tại (dùng cho pagination)
        var totalFiltered = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        // ===== lấy danh sách user theo page =====
        var pageUsers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.CreatedAt,
                Status = (u.Status ?? "pending"),
                u.BusinessRiskLevel
            })
            .ToListAsync();

        var businessIds = pageUsers.Select(x => x.Id).ToList();

        // ===== ServiceCount: Services.UserId =====
        var serviceMap = await _db.Services.AsNoTracking()
            .Where(s => businessIds.Contains(s.UserId))
            .GroupBy(s => s.UserId)
            .Select(g => new { BusinessUserId = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.BusinessUserId, x => x.Cnt);

        // ===== BookingCount: BookingOrders -> Services (ServiceId) -> Services.UserId =====
        var bookingMap = await _db.BookingOrders.AsNoTracking()
            .Join(_db.Services.AsNoTracking(),
                b => b.ServiceId,
                s => s.Id,
                (b, s) => new { BusinessUserId = s.UserId })
            .Where(x => businessIds.Contains(x.BusinessUserId))
            .GroupBy(x => x.BusinessUserId)
            .Select(g => new { BusinessUserId = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.BusinessUserId, x => x.Cnt);

        // ✅ Categories map: BusinessCategoryLinks (link) -> BusinessCategories (master)
        var categoryMap = await _db.BusinessCategoryLinks.AsNoTracking()
            .Where(l => businessIds.Contains(l.BusinessUserId))
            .Join(_db.BusinessCategories.AsNoTracking(),
                l => l.CategoryId,
                c => c.Id,
                (l, c) => new { l.BusinessUserId, CategoryName = c.Name })
            .GroupBy(x => x.BusinessUserId)
            .Select(g => new { BusinessUserId = g.Key, Cats = g.Select(x => x.CategoryName).Distinct().ToList() })
            .ToDictionaryAsync(x => x.BusinessUserId, x => x.Cats);

        // ===== build VM =====
        var vm = new AdminBusinessesIndexVm
        {
            Query = q,
            Status = status,
            Sort = sort,

            Page = page,
            PageSize = pageSize,

            TotalBusinesses = totalBusinesses,  // sidebar
            TotalItems = totalFiltered,         // table/pagination
            TotalPages = totalPages,
            FromItem = totalFiltered == 0 ? 0 : ((page - 1) * pageSize + 1),
            ToItem = totalFiltered == 0 ? 0 : Math.Min(page * pageSize, totalFiltered),

            Items = pageUsers.Select(x => new AdminBusinessesIndexVm.BusinessRowVm
            {
                Id = x.Id,
                Name = x.FullName ?? "(Chưa đặt tên)",
                OwnerName = x.FullName ?? "",
                OwnerEmail = x.Email ?? "",
                CreatedAt = x.CreatedAt,
                Status = x.Status,
                ServiceCount = serviceMap.TryGetValue(x.Id, out var sc) ? sc : 0,
                BookingCount = bookingMap.TryGetValue(x.Id, out var bc) ? bc : 0,
                Categories = categoryMap.TryGetValue(x.Id, out var cats) ? cats : new List<string>()
            }).ToList()
        };

        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.Sort = sort;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = totalPages;
        ViewBag.Total = totalFiltered;

        ViewBag.CountPending = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "pending");
        ViewBag.CountActive = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "active");
        ViewBag.CountSuspended = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "suspended");
        ViewBag.CountRejected = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "rejected");

        return View("~/Views/Admin/Businesses/Index.cshtml", vm);
    }

    // =========================
    // POST: /AdminBusinesses/Approve
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl = null)
    {
        var adminId = CurrentAdminId();
        if (!adminId.HasValue) return Forbid();

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == "business");
        if (u == null)
        {
            SetSwal("error", "Không tìm thấy", "Doanh nghiệp không tồn tại.");
            return RedirectSafe(returnUrl);
        }

        u.Status = "active";
        u.BusinessApprovedAt = DateTime.UtcNow;
        u.BusinessApprovedBy = adminId.Value;

        await _db.SaveChangesAsync();
        SetSwal("success", "Đã duyệt", "Doanh nghiệp đã được kích hoạt.");
        return RedirectSafe(returnUrl);
    }

    // =========================
    // POST: /AdminBusinesses/Reject
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? returnUrl = null)
    {
        var adminId = CurrentAdminId();
        if (!adminId.HasValue) return Forbid();

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == "business");
        if (u == null)
        {
            SetSwal("error", "Không tìm thấy", "Doanh nghiệp không tồn tại.");
            return RedirectSafe(returnUrl);
        }

        u.Status = "rejected";
        u.BusinessApprovedAt = DateTime.UtcNow;
        u.BusinessApprovedBy = adminId.Value;

        await _db.SaveChangesAsync();
        SetSwal("success", "Đã từ chối", "Doanh nghiệp đã bị từ chối.");
        return RedirectSafe(returnUrl);
    }

    // =========================
    // POST: /AdminBusinesses/Suspend
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string? returnUrl = null)
    {
        var adminId = CurrentAdminId();
        if (!adminId.HasValue) return Forbid();

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == "business");
        if (u == null)
        {
            SetSwal("error", "Không tìm thấy", "Doanh nghiệp không tồn tại.");
            return RedirectSafe(returnUrl);
        }

        u.Status = "suspended";
        u.BusinessApprovedAt = DateTime.UtcNow;
        u.BusinessApprovedBy = adminId.Value;

        await _db.SaveChangesAsync();
        SetSwal("success", "Đã tạm khóa", "Doanh nghiệp đã bị tạm khóa.");
        return RedirectSafe(returnUrl);
    }

    // =========================
    // POST: /AdminBusinesses/Restore
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, string? returnUrl = null)
    {
        var adminId = CurrentAdminId();
        if (!adminId.HasValue) return Forbid();

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == "business");
        if (u == null)
        {
            SetSwal("error", "Không tìm thấy", "Doanh nghiệp không tồn tại.");
            return RedirectSafe(returnUrl);
        }

        u.Status = "active";
        u.BusinessApprovedAt = DateTime.UtcNow;
        u.BusinessApprovedBy = adminId.Value;

        await _db.SaveChangesAsync();
        SetSwal("success", "Đã khôi phục", "Doanh nghiệp đã được khôi phục và kích hoạt.");
        return RedirectSafe(returnUrl);
    }

    // =========================
    // POST: /AdminBusinesses/BulkApproveLowRisk
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApproveLowRisk(int cap = 200, string? returnUrl = null)
    {
        var adminId = CurrentAdminId();
        if (!adminId.HasValue) return Forbid();

        cap = Math.Clamp(cap, 1, 2000);

        var list = await _db.Users
            .Where(u => u.Role == "business"
                        && (u.Status ?? "pending") == "pending"
                        && u.BusinessRiskLevel <= 0)
            .OrderBy(u => u.CreatedAt)
            .Take(cap)
            .ToListAsync();

        if (list.Count == 0)
        {
            SetSwal("info", "Không có gì để duyệt", "Không có doanh nghiệp pending thuộc nhóm low-risk.");
            return RedirectSafe(returnUrl);
        }

        var now = DateTime.UtcNow;
        foreach (var u in list)
        {
            u.Status = "active";
            u.BusinessApprovedAt = now;
            u.BusinessApprovedBy = adminId.Value;
        }

        await _db.SaveChangesAsync();
        SetSwal("success", "Đã duyệt hàng loạt", $"Đã duyệt {list.Count} doanh nghiệp (low-risk).");
        return RedirectSafe(returnUrl);
    }
}
