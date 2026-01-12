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
    // GET: /AdminBusinesses/Index?q=&status=&sort=&page=
    // status: all|pending|active|suspended|rejected
    // sort: newest|oldest|risk_desc|risk_asc|name|email
    // =========================
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status, string? sort, int page = 1)
    {
        const int pageSize = 12;

        q = (q ?? "").Trim();
        status = (status ?? "pending").Trim().ToLowerInvariant();
        sort = (sort ?? "newest").Trim().ToLowerInvariant();

        var allowedStatus = new[] { "all", "pending", "active", "suspended", "rejected" };
        if (!allowedStatus.Contains(status)) status = "pending";

        var query = _db.Users.AsNoTracking()
            .Where(u => u.Role == "business")
            .AsQueryable();

        if (status != "all")
            query = query.Where(u => (u.Status ?? "pending") == status);

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(u =>
                (u.FullName ?? "").Contains(q) ||
                (u.Email ?? "").Contains(q));
        }

        query = sort switch
        {
            "oldest"    => query.OrderBy(u => u.CreatedAt),
            "risk_desc" => query.OrderByDescending(u => u.BusinessRiskLevel).ThenByDescending(u => u.CreatedAt),
            "risk_asc"  => query.OrderBy(u => u.BusinessRiskLevel).ThenByDescending(u => u.CreatedAt),
            "name"      => query.OrderBy(u => u.FullName),
            "email"     => query.OrderBy(u => u.Email),
            _           => query.OrderByDescending(u => u.CreatedAt) // newest
        };

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminDashboardVm.BusinessRow
            {
                Id = u.Id,
                BusinessName = u.FullName ?? "(Chưa đặt tên)",
                OwnerEmail = u.Email ?? "",
                CreatedAt = u.CreatedAt,
                Status = u.Status ?? "pending",
                RiskLevel = u.BusinessRiskLevel
            })
            .ToListAsync();

        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.Sort = sort;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.Total = total;

        // số lượng theo trạng thái để hiện badge/filter nhanh
        ViewBag.CountPending = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "pending");
        ViewBag.CountActive = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "active");
        ViewBag.CountSuspended = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "suspended");
        ViewBag.CountRejected = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "business" && (u.Status ?? "pending") == "rejected");

    return View("~/Views/Admin/Business/Index.cshtml", items);
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
    // POST: /AdminBusinesses/Restore (khôi phục về active)
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
    // - Duyệt hàng loạt pending có risk <= 0
    // - cap để tránh bắn 1000 phát một lần (bé chỉnh tuỳ)
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
