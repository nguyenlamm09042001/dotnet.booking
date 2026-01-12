using booking.Data;
using booking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using booking.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters; 

namespace booking.Controllers;

[Authorize(Roles = "business")]
public class BusinessServicesController : Controller
{
    private const string V_INDEX  = "~/Views/Business/Services/Index.cshtml";
    private const string V_CREATE = "~/Views/Business/Services/Create.cshtml";
    private const string V_EDIT   = "~/Views/Business/Services/Edit.cshtml";

    private readonly AppDbContext _db;
    public BusinessServicesController(AppDbContext db) => _db = db;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var ok = await BusinessGuards.EnsureActiveBusiness(context, _db);
        if (!ok) return;

        await next();
    }
    private int? CurrentUserId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(s, out var id)) return id;
        return null;
    }

    private IActionResult RedirectSafe(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    // GET: /BusinessServices/Index?q=&cat=&active=&sort=&page=
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? cat, string? active, string? sort, int page = 1)
    {
        const int pageSize = 12;

        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        // ✅ SCOPE NGAY TỪ ĐẦU
        var query = _db.Services
            .AsNoTracking()
            .Where(x => x.UserId == businessUserId.Value)
            .AsQueryable();

        // search
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                x.Name.Contains(q) ||
                x.Category.Contains(q) ||
                x.Location.Contains(q));
        }

        // filter category (case-insensitive)
        if (!string.IsNullOrWhiteSpace(cat))
        {
            cat = cat.Trim();
            query = query.Where(x => x.Category.ToLower() == cat.ToLower());
        }

        // filter active
        if (active == "1") query = query.Where(x => x.IsActive);
        if (active == "0") query = query.Where(x => !x.IsActive);

        // sort
        query = sort switch
        {
            "newest"     => query.OrderByDescending(x => x.CreatedAt),
            "price_asc"  => query.OrderBy(x => x.Price),
            "price_desc" => query.OrderByDescending(x => x.Price),
            _            => query.OrderByDescending(x => x.CreatedAt),
        };

        // pagination
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        return View(V_INDEX, items);
    }



    // GET: /BusinessServices/Create
    [HttpGet]

    public IActionResult Create()
    {

        
        var m = new Service
        {
            IsActive = true
        };
        return View(V_CREATE, m);
    }

    // POST: /BusinessServices/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Service input)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        if (!ModelState.IsValid) return View(V_CREATE, input);

        // ✅ ÉP OWNER
        input.UserId = businessUserId.Value;

        input.CreatedAt = DateTime.Now;
        if (string.IsNullOrWhiteSpace(input.Location)) input.Location = "—";

        _db.Services.Add(input);
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Thành công";
        TempData["SwalMessage"] = "Đã tạo dịch vụ mới.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /BusinessServices/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        // ✅ CHỈ LẤY SERVICE CỦA DOANH NGHIỆP
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.UserId == businessUserId.Value);
        if (s == null) return NotFound();

        return View(V_EDIT, s);
    }

    // POST: /BusinessServices/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Service input)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        if (!ModelState.IsValid) return View(V_EDIT, input);

        // ✅ CHỈ UPDATE SERVICE CỦA DOANH NGHIỆP
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == input.Id && x.UserId == businessUserId.Value);
        if (s == null) return NotFound();

        s.Name = input.Name;
        s.Category = input.Category;
        s.Description = input.Description;
        s.Price = input.Price;
        s.DurationMinutes = input.DurationMinutes;
        s.Location = input.Location;
        s.IsActive = input.IsActive;

        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã lưu";
        TempData["SwalMessage"] = "Cập nhật dịch vụ thành công.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /BusinessServices/Toggle
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, string? returnUrl = null)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        // ✅ CHỈ TOGGLE SERVICE CỦA DOANH NGHIỆP
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.UserId == businessUserId.Value);
        if (s == null) return NotFound();

        s.IsActive = !s.IsActive;
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "OK";
        TempData["SwalMessage"] = s.IsActive ? "Đã kích hoạt dịch vụ." : "Đã tạm dừng dịch vụ.";

        return RedirectSafe(returnUrl);
    }

    // POST: /BusinessServices/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl = null)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        // ✅ CHỈ XOÁ SERVICE CỦA DOANH NGHIỆP
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.UserId == businessUserId.Value);
        if (s == null) return NotFound();

        _db.Services.Remove(s);
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã xoá";
        TempData["SwalMessage"] = "Đã xoá dịch vụ.";

        return RedirectSafe(returnUrl);
    }
}
