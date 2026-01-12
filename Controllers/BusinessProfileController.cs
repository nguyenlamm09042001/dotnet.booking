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
    // GET: /BusinessProfile/Index
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var u = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (u == null) return NotFound();

        return View("~/Views/Business/Profile/Index.cshtml", u);
    }

    // POST: /BusinessProfile/Index
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(User input)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null) return NotFound();

        if (!ModelState.IsValid)
            return View("~/Views/Business/Profile/Index.cshtml", input);

        // cập nhật các field cho phép
        u.FullName = input.FullName;
        u.Email = input.Email;

        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã lưu";
        TempData["SwalMessage"] = "Cập nhật hồ sơ doanh nghiệp thành công.";

        return RedirectToAction(nameof(Index));
    }
}
