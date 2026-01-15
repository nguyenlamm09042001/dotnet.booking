using booking.Data;
using booking.Models;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using booking.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace booking.Controllers;

[Authorize(Roles = "business")]
public class BusinessProfileController : Controller
{
    private readonly AppDbContext _db;
    public BusinessProfileController(AppDbContext db) => _db = db;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var ok = await BusinessGuards.EnsureActiveBusiness(context, _db);
        if (!ok) return;

        await next();
    }

    private const string V_INDEX = "~/Views/Business/Profile/Index.cshtml";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return NotFound();

        var vm = new BusinessProfileVm
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Avatar = u.Avatar,
            Role = u.Role
        };

        return View(V_INDEX, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BusinessProfileVm input)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return NotFound();

        // giữ lại role + avatar để render đúng khi lỗi
        input.Role = u.Role;
        input.Avatar = u.Avatar;
        input.Id = u.Id;

        if (!ModelState.IsValid)
            return View(V_INDEX, input);

        // ✅ update text fields
        u.FullName = input.FullName;
        u.Email = input.Email;

        // ✅ upload avatar nếu có
        if (input.AvatarFile != null && input.AvatarFile.Length > 0)
        {
            const long maxBytes = 3 * 1024 * 1024;
            if (input.AvatarFile.Length > maxBytes)
            {
                ModelState.AddModelError("", "Ảnh quá lớn (tối đa 3MB).");
                return View(V_INDEX, input);
            }

            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(input.AvatarFile.FileName).ToLowerInvariant();
            if (!allowedExt.Contains(ext))
            {
                ModelState.AddModelError("", "Chỉ chấp nhận ảnh JPG, JPEG, PNG, WEBP.");
                return View(V_INDEX, input);
            }

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var fileName = $"biz_{u.Id}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await input.AvatarFile.CopyToAsync(stream);
            }

            // xóa ảnh cũ
            if (!string.IsNullOrWhiteSpace(u.Avatar))
            {
                var oldPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    u.Avatar.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
                );
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            u.Avatar = $"/uploads/avatars/{fileName}";
        }

        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã lưu";
        TempData["SwalMessage"] = "Cập nhật hồ sơ doanh nghiệp thành công.";

        return RedirectToAction(nameof(Index));
    }
}
