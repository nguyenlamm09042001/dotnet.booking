using booking.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

[Authorize(Roles = "admin")]
public class AdminUsersController : Controller
{
    private readonly AppDbContext _db;

    public AdminUsersController(AppDbContext db)
    {
        _db = db;
    }

    // GET: /AdminUsers/Index?q=
    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        q = (q ?? "").Trim();

        var usersQuery = _db.Users
            .AsNoTracking()
            .Where(u => u.Role != "admin"); // CHỈ user + business

        // ✅ lọc theo q (tên/email/role/id)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var qLower = q.ToLowerInvariant();

            usersQuery = usersQuery.Where(u =>
                (u.FullName ?? "").ToLower().Contains(qLower) ||
                (u.Email ?? "").ToLower().Contains(qLower) ||
                (u.Role ?? "").ToLower().Contains(qLower) ||
                u.Id.ToString().Contains(q)
            );
        }

        var users = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        ViewBag.Q = q;

        return View("~/Views/Admin/User/Index.cshtml", users);
    }

    // POST: /AdminUsers/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return RedirectToAction(nameof(Index));

        if ((user.Role ?? "").Trim().ToLowerInvariant() == "admin")
        {
            TempData["SwalType"] = "warning";
            TempData["SwalTitle"] = "Không thể xóa";
            TempData["SwalMessage"] = "Không được xóa tài khoản admin.";
            return RedirectToAction(nameof(Index));
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã xóa";
        TempData["SwalMessage"] = "Xóa người dùng thành công.";

        return RedirectToAction(nameof(Index));
    }
}
