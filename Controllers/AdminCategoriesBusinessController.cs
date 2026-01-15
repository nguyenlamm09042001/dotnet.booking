using booking.Data;
using booking.Models;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

[Authorize(Roles = "admin")]
[Route("Admin/Businesses/CategoriesBusiness")]
public class AdminCategoriesBusinessController : Controller
{
    private readonly AppDbContext _db;

    private const string V_INDEX  = "~/Views/Admin/Businesses/CategoriesBusiness/Index.cshtml";
    private const string V_CREATE = "~/Views/Admin/Businesses/CategoriesBusiness/Create.cshtml";
    private const string V_EDIT   = "~/Views/Admin/Businesses/CategoriesBusiness/Edit.cshtml";

    public AdminCategoriesBusinessController(AppDbContext db)
    {
        _db = db;
    }

    // GET: /Admin/Businesses/Categories?q=&status=&page=
    [HttpGet("")]
  public async Task<IActionResult> Index(string? q, string? status, string? sort, int page = 1)
    {
        const int pageSize = 12;

        q = (q ?? "").Trim();
        status = (status ?? "all").Trim().ToLowerInvariant();   // all|active|inactive
        sort = (sort ?? "newest").Trim().ToLowerInvariant();    // newest|oldest|name_asc|name_desc

        if (status != "all" && status != "active" && status != "inactive") status = "all";
        if (sort != "newest" && sort != "oldest" && sort != "name_asc" && sort != "name_desc") sort = "newest";
        if (page < 1) page = 1;

        // ===== Stats =====
        var total = await _db.BusinessCategories.CountAsync();
        var active = await _db.BusinessCategories.CountAsync(x => x.IsActive);
        var inactive = total - active;

        // ===== Query =====
        var query = _db.BusinessCategories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.Name.Contains(q) ||
                x.Code.Contains(q));
        }

        if (status == "active") query = query.Where(x => x.IsActive);
        if (status == "inactive") query = query.Where(x => !x.IsActive);

        query = sort switch
        {
            "oldest" => query.OrderBy(x => x.Id),
            "name_asc" => query.OrderBy(x => x.Name),
            "name_desc" => query.OrderByDescending(x => x.Name),
            _ => query.OrderByDescending(x => x.Id) // newest
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages <= 0) totalPages = 1;
        if (page > totalPages) page = totalPages;

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminBusinessCategoriesIndexVm.BusinessCategoryRow
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                LinkedBusinesses = 0 // nếu muốn đếm link, mình sẽ bổ sung sau
            })
            .ToListAsync();

        var vm = new AdminBusinessCategoriesIndexVm
        {
            Q = q,
            Status = status,
            Sort = sort,

            TotalCategories = total,
            ActiveCategories = active,
            InactiveCategories = inactive,

            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,

            Items = rows
        };

        return View(V_INDEX, vm);
    }

    // GET: /Admin/Businesses/Categories/Create
    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View(V_CREATE, new BusinessCategory());
    }

    // POST: /Admin/Businesses/Categories/Create
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusinessCategory input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            ModelState.AddModelError(nameof(input.Name), "Vui lòng nhập tên danh mục.");

        if (string.IsNullOrWhiteSpace(input.Code))
            ModelState.AddModelError(nameof(input.Code), "Vui lòng nhập code (vd: hair, beauty...).");

        // code unique (ignore-case)
        if (!string.IsNullOrWhiteSpace(input.Code))
        {
            var code = input.Code.Trim().ToLowerInvariant();
            var exists = await _db.BusinessCategories.AnyAsync(x => x.Code.ToLower() == code);
            if (exists) ModelState.AddModelError(nameof(input.Code), "Code đã tồn tại.");
        }

        if (!ModelState.IsValid) return View(V_CREATE, input);

        input.Name = input.Name.Trim();
        input.Code = input.Code.Trim().ToLowerInvariant();
        input.IsActive = true;
        input.CreatedAt = DateTime.Now;

        _db.BusinessCategories.Add(input);
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Thành công";
        TempData["SwalMessage"] = "Đã thêm danh mục.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Businesses/Categories/Edit/5
    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var cat = await _db.BusinessCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (cat == null) return NotFound();

        return View(V_EDIT, cat);
    }

    // POST: /Admin/Businesses/Categories/Edit/5
    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BusinessCategory input)
    {
        var cat = await _db.BusinessCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (cat == null) return NotFound();

        if (string.IsNullOrWhiteSpace(input.Name))
            ModelState.AddModelError(nameof(input.Name), "Vui lòng nhập tên danh mục.");

        if (string.IsNullOrWhiteSpace(input.Code))
            ModelState.AddModelError(nameof(input.Code), "Vui lòng nhập code.");

        // code unique (ignore-case) trừ chính nó
        if (!string.IsNullOrWhiteSpace(input.Code))
        {
            var code = input.Code.Trim().ToLowerInvariant();
            var exists = await _db.BusinessCategories.AnyAsync(x => x.Id != id && x.Code.ToLower() == code);
            if (exists) ModelState.AddModelError(nameof(input.Code), "Code đã tồn tại.");
        }

        if (!ModelState.IsValid) return View(V_EDIT, input);

        cat.Name = input.Name.Trim();
        cat.Code = input.Code.Trim().ToLowerInvariant();
        cat.IsActive = input.IsActive;

        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Thành công";
        TempData["SwalMessage"] = "Đã cập nhật danh mục.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Businesses/Categories/Toggle/5
   [HttpPost("Toggle/{id:int}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Toggle(int id, string? returnUrl)
{
    var cat = await _db.BusinessCategories.FirstOrDefaultAsync(x => x.Id == id);
    if (cat == null) return NotFound();

    cat.IsActive = !cat.IsActive;
    await _db.SaveChangesAsync();

    TempData["SwalType"] = "success";
    TempData["SwalTitle"] = "OK";
    TempData["SwalMessage"] = cat.IsActive ? "Đã bật danh mục." : "Đã tắt danh mục.";

    // ✅ quay lại đúng URL trước đó (có q/status/sort/page)
    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        return Redirect(returnUrl);

    return RedirectToAction(nameof(Index));
}

}
