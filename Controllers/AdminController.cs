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
// GET: /Admin hoặc /Admin/Index
[HttpGet]
public async Task<IActionResult> Index(string? q, int page = 1)
{
    // ✅ Nếu gõ search ở dashboard -> chuyển qua trang Users để hiển thị kết quả
    if (!string.IsNullOrWhiteSpace(q))
        return RedirectToAction("Index", "AdminUsers", new { q = q.Trim() });

    var now = DateTime.Now;
    var from7d = now.AddDays(-7);

    // ===== KPI Users =====
    var totalUsers = await _db.Users.AsNoTracking().CountAsync();
    var newUsers7d = await _db.Users.AsNoTracking().CountAsync(u => u.CreatedAt >= from7d);

    // ===== KPI Businesses (role=business) =====
    var totalBusinesses = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "business");

    // Nếu bé chưa có BusinessStatus thì để pendingBusinesses=0 / activeBusinesses=totalBusinesses
    // Còn nếu có cột BusinessStatus thì dùng 2 dòng dưới (mở comment)
    // var pendingBusinesses = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "business" && (u.BusinessStatus ?? "pending") == "pending");
    // var activeBusinesses  = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "business" && (u.BusinessStatus ?? "pending") == "active");

    var pendingBusinesses = 0;
    var activeBusinesses = totalBusinesses;

    // ✅ systemAlerts (bé đang dùng BookingOrders)
    var pendingTooLong = now.AddHours(-24);
    var systemAlerts = await _db.BookingOrders.AsNoTracking()
        .CountAsync(b => b.Status == "pending" && b.CreatedAt <= pendingTooLong);

    // ✅ recentUsers
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

    var vm = new AdminDashboardVm
    {
        TotalUsers = totalUsers,
        NewUsers7d = newUsers7d,
        TotalBusinesses = totalBusinesses,
        PendingBusinesses = pendingBusinesses,
        ActiveBusinesses = activeBusinesses,
        SystemAlerts = systemAlerts,     // ✅ hết lỗi
        RecentUsers = recentUsers        // ✅ hết lỗi
    };

    // ✅ đúng path view của bé:
    return View("~/Views/Admin/Home/Index.cshtml", vm);
}



    // ========= MENU ROUTES (để khỏi 404) =========
    [HttpGet]
    public IActionResult Users()
    {
        // TODO: làm trang quản lý người dùng sau
        return View();
    }

    [HttpGet]
    public IActionResult Businesses()
    {
        // TODO: làm trang quản lý doanh nghiệp sau (khi có bảng Businesses)
        return View();
    }

    // ========= DELETE USER (an toàn) =========
    // UI bên cshtml đang post về action này.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return RedirectToAction(nameof(Index));

        // không cho xóa admin (đỡ tự bắn vào chân)
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
