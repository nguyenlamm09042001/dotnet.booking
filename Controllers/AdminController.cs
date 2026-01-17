using booking.Data;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

[Authorize(Roles = "admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // GET: /Admin hoặc /Admin/Index
    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        if (!string.IsNullOrWhiteSpace(q))
            return RedirectToAction("Index", "AdminUsers", new { q = q.Trim() });

        var now = DateTime.UtcNow;
        var from7d = now.AddDays(-7);

        // ===== KPI Users =====
        var totalUsers = await _db.Users.AsNoTracking().CountAsync(u => u.Role != "admin");
        var newUsers7d = await _db.Users.AsNoTracking().CountAsync(u => u.Role != "admin" && u.CreatedAt >= from7d);

        // ===== KPI Businesses =====
        var totalBusinesses = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "business");
        var pendingBusinesses = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "pending");
        var activeBusinesses = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "active");

        // ===== Alerts (ví dụ pending booking quá 24h) =====
        var pendingTooLong = now.AddHours(-24);
        var systemAlerts = await _db.BookingOrders.AsNoTracking()
            .CountAsync(b => b.Status == "pending" && b.CreatedAt <= pendingTooLong);

        // ===== Recent users preview =====
        var recentUsers = await _db.Users.AsNoTracking()
            .Where(u => u.Role != "admin")
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminDashboardVm.UserRow
            {
                Id = u.Id,
                FullName = u.FullName ?? "",
                Email = u.Email ?? "",
                Role = u.Role ?? "",
                CreatedAt = u.CreatedAt
            })
            .Take(5)
            .ToListAsync();

        // ✅ Ads pending preview (5 đơn)


        // ✅ ===== Pending businesses preview TOP 10 =====
        var pendingBizPreview = await _db.Users.AsNoTracking()
            .Where(u => u.Role == "business" && (u.Status ?? "pending") == "pending")
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminDashboardVm.BusinessRow
            {
                Id = u.Id,
                BusinessName = u.FullName ?? "(Chưa đặt tên)",
                OwnerEmail = u.Email ?? "",
                CreatedAt = u.CreatedAt,
                Status = u.Status ?? "pending",
                RiskLevel = u.BusinessRiskLevel,
                Categories = new List<string>() // sẽ fill dưới
            })
            .Take(10)
            .ToListAsync();

        var pendingIds = pendingBizPreview.Select(x => x.Id).ToList();

        if (pendingIds.Count > 0)
        {
            var catPairs = await (
                from l in _db.BusinessCategoryLinks.AsNoTracking()
                join c in _db.BusinessCategories.AsNoTracking()
                    on l.CategoryId equals c.Id
                where pendingIds.Contains(l.BusinessUserId)
                select new
                {
                    l.BusinessUserId,
                    CatName = c.Name
                }
            ).ToListAsync();

            var catMap = catPairs
                .GroupBy(x => x.BusinessUserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.CatName).Distinct().ToList()
                );

            foreach (var b in pendingBizPreview)
            {
                b.Categories = catMap.TryGetValue(b.Id, out var cats)
                    ? cats
                    : new List<string>();
            }
        }

        var vm = new AdminDashboardVm
        {
            TotalUsers = totalUsers,
            NewUsers7d = newUsers7d,
            TotalBusinesses = totalBusinesses,
            PendingBusinesses = pendingBusinesses,
            ActiveBusinesses = activeBusinesses,
            SystemAlerts = systemAlerts,
            RecentUsers = recentUsers,
            PendingBusinessesPreview = pendingBizPreview
        };

        vm.PendingAdsPreview = await _db.MarketingOrders
    .AsNoTracking()
    .Where(x => x.Status == "pending")
    .OrderByDescending(x => x.PaidAt ?? x.CreatedAt)
    .Take(5)
    .Select(x => new AdminDashboardVm.PendingAdRow
    {
        Id = x.Id,
        Code = x.Code,
        BusinessId = x.BusinessId,
        BusinessName = _db.Users
            .Where(u => u.Id == x.BusinessId)
            .Select(u => u.FullName ?? u.Email)
            .FirstOrDefault() ?? "—",
        Amount = x.Amount,
        Days = x.Days,
        StartAt = x.StartAt,
        PaidAt = x.PaidAt,
        Status = x.Status
    })
    .ToListAsync();


        return View("~/Views/Admin/Home/Index.cshtml", vm);
    }

    // ========= MENU ROUTES (để khỏi 404) =========
    [HttpGet]
    public IActionResult Users()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Businesses()
    {
        return View();
    }

    // ========= DELETE USER (an toàn) =========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return RedirectToAction(nameof(Index));

        if ((u.Role ?? "").ToLower() == "admin")
        {
            TempData["SwalType"] = "warning";
            TempData["SwalTitle"] = "Không thể xóa";
            TempData["SwalMessage"] = "Không được xóa tài khoản admin.";
            return RedirectToAction(nameof(Index));
        }

        _db.Users.Remove(u);
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã xóa";
        TempData["SwalMessage"] = "Xóa người dùng thành công.";

        return RedirectToAction(nameof(Index));
    }
}
