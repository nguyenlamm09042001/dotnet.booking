using booking.Data;
using booking.Models;
using booking.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

    private async Task LoadServiceCategoriesAsync(int? selectedId = null)
{
    var items = await _db.ServiceCategories
        .AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Name)
        .Select(x => new { x.Id, x.Name })
        .ToListAsync();

    ViewBag.ServiceCategoryOptions =
        new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            items, "Id", "Name", selectedId
        );
}


    // =========================
    // INDEX
    // =========================
    // GET: /BusinessServices/Index?q=&cat=&active=&sort=&page=
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? cat, string? active, string? sort, int page = 1)
    {
        const int pageSize = 12;

        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        var query = _db.Services
            .AsNoTracking()
            .Where(x => x.UserId == businessUserId.Value)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                x.Name.Contains(q) ||
                x.Category.Contains(q) ||
                x.Location.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(cat))
        {
            cat = cat.Trim();
            query = query.Where(x => x.Category.ToLower() == cat.ToLower());
        }

        if (active == "1") query = query.Where(x => x.IsActive);
        if (active == "0") query = query.Where(x => !x.IsActive);

        query = sort switch
        {
            "newest"     => query.OrderByDescending(x => x.CreatedAt),
            "price_asc"  => query.OrderBy(x => x.Price),
            "price_desc" => query.OrderByDescending(x => x.Price),
            _            => query.OrderByDescending(x => x.CreatedAt),
        };

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

    // =========================
    // CREATE
    // =========================
    // GET: /BusinessServices/Create
   [HttpGet]
public async Task<IActionResult> Create()
{
    await LoadServiceCategoriesAsync();
    var m = new Service { IsActive = true };
    return View(V_CREATE, m);
}


  [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(Service input, IFormFile? thumbFile)
{
    var businessUserId = CurrentUserId();
    if (!businessUserId.HasValue) return Forbid();

    input.UserId = businessUserId.Value;
    input.CreatedAt = DateTime.Now;

    if (string.IsNullOrWhiteSpace(input.Location))
        input.Location = "—";

    input.Name = (input.Name ?? "").Trim();
    input.Description = (input.Description ?? "").Trim();
    input.Location = (input.Location ?? "").Trim();

    // ✅ VALIDATE: phải chọn danh mục
    if (!input.ServiceCategoryId.HasValue)
    {
        ModelState.AddModelError(
            nameof(input.ServiceCategoryId),
            "Vui lòng chọn danh mục dịch vụ."
        );
    }

    // ❌ KHÔNG dùng Category text nữa
    // input.Category = ...

    // ❌ KHÔNG load dropdown ở đầu
    // await LoadServiceCategoriesAsync();

    if (!ModelState.IsValid)
    {
        // ✅ CHỈ load dropdown khi trả View
        await LoadServiceCategoriesAsync(input.ServiceCategoryId);

        TempData["SwalType"] = "error";
        TempData["SwalTitle"] = "Chưa hợp lệ";
        TempData["SwalMessage"] = "Vui lòng kiểm tra lại các trường bắt buộc.";
        return View(V_CREATE, input);
    }

    // =========================
    // UPLOAD THUMBNAIL (GIỮ NGUYÊN LOGIC CŨ)
    // =========================
    if (thumbFile != null && thumbFile.Length > 0)
    {
        const long maxBytes = 3 * 1024 * 1024;
        if (thumbFile.Length > maxBytes)
        {
            await LoadServiceCategoriesAsync(input.ServiceCategoryId);

            TempData["SwalType"] = "error";
            TempData["SwalTitle"] = "Ảnh quá lớn";
            TempData["SwalMessage"] = "Ảnh tối đa 3MB.";
            return View(V_CREATE, input);
        }

        var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(thumbFile.FileName).ToLowerInvariant();
        if (!allowedExt.Contains(ext))
        {
            await LoadServiceCategoriesAsync(input.ServiceCategoryId);

            TempData["SwalType"] = "error";
            TempData["SwalTitle"] = "Sai định dạng";
            TempData["SwalMessage"] = "Chỉ chấp nhận JPG, JPEG, PNG, WEBP.";
            return View(V_CREATE, input);
        }

        var uploadsDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            "uploads",
            "services"
        );

        if (!Directory.Exists(uploadsDir))
            Directory.CreateDirectory(uploadsDir);

        var fileName = $"svc_{businessUserId.Value}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await thumbFile.CopyToAsync(stream);
        }

        input.Thumbnail = $"/uploads/services/{fileName}";
    }

    try
    {
        _db.Services.Add(input);
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Thành công";
        TempData["SwalMessage"] = "Đã tạo dịch vụ mới.";

        return RedirectToAction(nameof(Index));
    }
    catch (DbUpdateException)
    {
        await LoadServiceCategoriesAsync(input.ServiceCategoryId);

        TempData["SwalType"] = "error";
        TempData["SwalTitle"] = "Lỗi lưu dữ liệu";
        TempData["SwalMessage"] = "Không thể lưu dịch vụ. Vui lòng thử lại.";
        return View(V_CREATE, input);
    }
}


    // =========================
    // EDIT
    // =========================
    // GET: /BusinessServices/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.UserId == businessUserId.Value);
        if (s == null) return NotFound();

        return View(V_EDIT, s);
    }

    // POST: /BusinessServices/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Service input, IFormFile? thumbFile)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        if (!ModelState.IsValid) return View(V_EDIT, input);

        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == input.Id && x.UserId == businessUserId.Value);
        if (s == null) return NotFound();

        s.Name = (input.Name ?? "").Trim();
        s.Category = (input.Category ?? "").Trim();
        s.Description = (input.Description ?? "").Trim();
        s.Price = input.Price;
        s.DurationMinutes = input.DurationMinutes;
        s.Location = (input.Location ?? "").Trim();
        s.IsActive = input.IsActive;

        // ✅ Upload thumbnail mới (nếu có)
        if (thumbFile != null && thumbFile.Length > 0)
        {
            const long maxBytes = 3 * 1024 * 1024;
            if (thumbFile.Length > maxBytes)
            {
                TempData["SwalType"] = "error";
                TempData["SwalTitle"] = "Ảnh quá lớn";
                TempData["SwalMessage"] = "Ảnh tối đa 3MB.";
                input.Thumbnail = s.Thumbnail;
                return View(V_EDIT, input);
            }

            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(thumbFile.FileName).ToLowerInvariant();
            if (!allowedExt.Contains(ext))
            {
                TempData["SwalType"] = "error";
                TempData["SwalTitle"] = "Sai định dạng";
                TempData["SwalMessage"] = "Chỉ chấp nhận JPG, JPEG, PNG, WEBP.";
                input.Thumbnail = s.Thumbnail;
                return View(V_EDIT, input);
            }

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "services");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var fileName = $"svc_{businessUserId.Value}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await thumbFile.CopyToAsync(stream);
            }

            // xoá ảnh cũ (nếu có)
            if (!string.IsNullOrWhiteSpace(s.Thumbnail))
            {
                var oldPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    s.Thumbnail.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
                );

                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            s.Thumbnail = $"/uploads/services/{fileName}";
        }

        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã lưu";
        TempData["SwalMessage"] = "Cập nhật dịch vụ thành công.";

        return RedirectToAction(nameof(Index));
    }

    // =========================
    // TOGGLE
    // =========================
    // POST: /BusinessServices/Toggle
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, string? returnUrl = null)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.UserId == businessUserId.Value);
        if (s == null) return NotFound();

        s.IsActive = !s.IsActive;
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "OK";
        TempData["SwalMessage"] = s.IsActive ? "Đã kích hoạt dịch vụ." : "Đã tạm dừng dịch vụ.";

        return RedirectSafe(returnUrl);
    }

    // =========================
    // DELETE
    // =========================
    // POST: /BusinessServices/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl = null)
    {
        var businessUserId = CurrentUserId();
        if (!businessUserId.HasValue) return Forbid();

        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.UserId == businessUserId.Value);
        if (s == null) return NotFound();

        // xoá file ảnh (nếu có) trước khi xoá record
        if (!string.IsNullOrWhiteSpace(s.Thumbnail))
        {
            var oldPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                s.Thumbnail.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        _db.Services.Remove(s);
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã xoá";
        TempData["SwalMessage"] = "Đã xoá dịch vụ.";

        return RedirectSafe(returnUrl);
    }
}
