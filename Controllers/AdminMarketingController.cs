using booking.Data;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

[Authorize(Roles = "admin")]
public class AdminMarketingController : Controller
{
    private readonly AppDbContext _db;

    public AdminMarketingController(AppDbContext db)
    {
        _db = db;
    }

    private const string VIEW_INDEX = "~/Views/Admin/Businesses/Marketing/Index.cshtml";

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    // GET: /AdminMarketing/Index?q=...&status=pending
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status)
    {
        ViewBag.Q = q?.Trim() ?? "";
        ViewBag.Status = status?.Trim() ?? "";

        var baseQuery = _db.MarketingOrders.AsNoTracking();

        // Filter status
     // Filter status
if (!string.IsNullOrWhiteSpace(status))
{
    var st = status.Trim().ToLower();
    baseQuery = baseQuery.Where(x => (x.Status ?? "pending").ToLower() == st);
}


        // Search: code hoặc businessId hoặc business name/email
        if (!string.IsNullOrWhiteSpace(q))
        {
            var k = q.Trim();

            if (int.TryParse(k, out var num))
            {
                baseQuery = baseQuery.Where(x => x.BusinessId == num || x.Id == num);
            }
            else
            {
                baseQuery = baseQuery.Where(x =>
                    (x.Code != null && x.Code.Contains(k)) ||
                    _db.Users.Any(u => u.Id == x.BusinessId &&
                        ((u.FullName != null && u.FullName.Contains(k)) ||
                         (u.Email != null && u.Email.Contains(k))))
                );
            }
        }

        var rows = await (
            from o in baseQuery
            join u in _db.Users.AsNoTracking()
                on o.BusinessId equals u.Id into gj
            from u in gj.DefaultIfEmpty()
            orderby (o.PaidAt ?? o.CreatedAt) descending
            select new AdminMarketingIndexVm.Row
            {
                Id = o.Id,
                Code = o.Code,
                BusinessId = o.BusinessId,
                BusinessName = (u != null ? (u.FullName ?? u.Email) : "—"),
                Amount = o.Amount,
                Days = o.Days,
                StartAt = o.StartAt,
                PaidAt = o.PaidAt,
                Status = o.Status ?? "pending",
                CreatedAt = o.CreatedAt
            }
        ).ToListAsync();

        var vm = new AdminMarketingIndexVm
        {
            Q = (string)ViewBag.Q,
            Status = (string)ViewBag.Status,
            Rows = rows
        };

        return View(VIEW_INDEX, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl)
    {
        var order = await _db.MarketingOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (order == null)
        {
            TempData["SwalType"] = "warning";
            TempData["SwalTitle"] = "Không tìm thấy";
            TempData["SwalMessage"] = "Đơn Ads không tồn tại.";
            return Redirect(SafeReturn(returnUrl));
        }

        order.Status = "approved";
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã duyệt";
        TempData["SwalMessage"] = $"Đơn Ads {order.Code} đã được duyệt.";

        return Redirect(SafeReturn(returnUrl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? returnUrl)
    {
        var order = await _db.MarketingOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (order == null)
        {
            TempData["SwalType"] = "warning";
            TempData["SwalTitle"] = "Không tìm thấy";
            TempData["SwalMessage"] = "Đơn Ads không tồn tại.";
            return Redirect(SafeReturn(returnUrl));
        }

        order.Status = "rejected";
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã từ chối";
        TempData["SwalMessage"] = $"Đơn Ads {order.Code} đã bị từ chối.";

        return Redirect(SafeReturn(returnUrl));
    }

    private string SafeReturn(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return returnUrl;

        return Url.Action("Index", "AdminMarketing") ?? "/AdminMarketing/Index";
    }

    // GET: /AdminMarketing/Details/5
[HttpGet]
public async Task<IActionResult> Details(int id)
{
    var row = await (
        from o in _db.MarketingOrders.AsNoTracking()
        where o.Id == id
        join u in _db.Users.AsNoTracking()
            on o.BusinessId equals u.Id into gj
        from u in gj.DefaultIfEmpty()
        select new AdminMarketingDetailsVm
        {
            Id = o.Id,
            Code = o.Code,
            BusinessId = o.BusinessId,
            BusinessName = (u != null ? (u.FullName ?? u.Email) : "—"),
            BusinessEmail = (u != null ? (u.Email ?? "") : ""),
            Amount = o.Amount,
            Days = o.Days,
            StartAt = o.StartAt,
            PaidAt = o.PaidAt,
            Status = o.Status ?? "pending",
            CreatedAt = o.CreatedAt
        }
    ).FirstOrDefaultAsync();

    if (row == null) return NotFound();

    return View("~/Views/Admin/Business/Marketing/Details.cshtml", row);
}

}
