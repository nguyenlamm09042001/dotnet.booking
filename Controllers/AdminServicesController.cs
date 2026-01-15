using booking.Data;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

[Authorize(Roles = "admin")]
public class AdminServicesController : Controller
{
    private const string V_INDEX = "~/Views/Admin/Businesses/Services/Index.cshtml";

    private readonly AppDbContext _db;
    public AdminServicesController(AppDbContext db) => _db = db;

    private void SetSwal(string type, string title, string message, string? redirect = null)
    {
        TempData["SwalType"] = type;
        TempData["SwalTitle"] = title;
        TempData["SwalMessage"] = message;

        if (!string.IsNullOrWhiteSpace(redirect))
            TempData["SwalRedirect"] = redirect;
    }

    // GET: /AdminServices/Index?q=&status=&cat=&sort=&page=
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status, string? cat, string? sort, int page = 1)
    {
        const int pageSize = 12;

        q = (q ?? "").Trim();
        status = (status ?? "all").Trim().ToLower(); // all|active|inactive
        cat = (cat ?? "").Trim();
        sort = (sort ?? "newest").Trim().ToLower();
        if (page < 1) page = 1;

        var allowedStatus = new[] { "all", "active", "inactive" };
        if (!allowedStatus.Contains(status)) status = "all";

        var allowedSort = new[] { "newest", "oldest", "name_asc", "name_desc", "price_asc", "price_desc" };
        if (!allowedSort.Contains(sort)) sort = "newest";

        // ===== 1) categories (await tuần tự)
        var categories = await _db.Services
            .AsNoTracking()
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        // ===== 2) stats (await tuần tự)
        var totalServices = await _db.Services.CountAsync();
        var activeServices = await _db.Services.CountAsync(x => x.IsActive);
        var inactiveServices = await _db.Services.CountAsync(x => !x.IsActive);

        // ===== 3) query chính (lọc/search/sort)
        var query = _db.Services.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.Name.Contains(q) ||
                x.Category.Contains(q) ||
                x.Location.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(cat))
            query = query.Where(x => x.Category == cat);

        if (status == "active") query = query.Where(x => x.IsActive);
        if (status == "inactive") query = query.Where(x => !x.IsActive);

        query = sort switch
        {
            "oldest" => query.OrderBy(x => x.CreatedAt),
            "name_asc" => query.OrderBy(x => x.Name),
            "name_desc" => query.OrderByDescending(x => x.Name),
            "price_asc" => query.OrderBy(x => x.Price),
            "price_desc" => query.OrderByDescending(x => x.Price),
            _ => query.OrderByDescending(x => x.CreatedAt),
        };

        // ===== 4) paging (await tuần tự)
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new AdminServicesIndexVm
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,

            Q = q,
            Status = status,
            Cat = cat,
            Sort = sort,
            Categories = categories,

            TotalServices = totalServices,
            ActiveServices = activeServices,
            InactiveServices = inactiveServices
        };

        return View(V_INDEX, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, string? returnUrl = null)
    {
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null)
        {
            SetSwal("error", "Không tìm thấy", "Dịch vụ không tồn tại hoặc đã bị xoá.");
            return Redirect(returnUrl ?? Url.Action("Index")!);
        }

        s.IsActive = !s.IsActive;
        await _db.SaveChangesAsync();

        SetSwal(
            "success",
            s.IsActive ? "Đã mở dịch vụ" : "Đã khoá dịch vụ",
            s.IsActive ? "Dịch vụ đã được kích hoạt trở lại." : "Dịch vụ đã bị tạm khoá (ẩn khỏi user)."
        );

        return Redirect(returnUrl ?? Url.Action("Index")!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl = null)
    {
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null)
        {
            SetSwal("error", "Không tìm thấy", "Dịch vụ không tồn tại hoặc đã bị xoá.");
            return Redirect(returnUrl ?? Url.Action("Index")!);
        }

        _db.Services.Remove(s);
        await _db.SaveChangesAsync();

        SetSwal("success", "Đã xoá", $"Đã xoá dịch vụ #{id} khỏi hệ thống.");
        return Redirect(returnUrl ?? Url.Action("Index")!);
    }
}
