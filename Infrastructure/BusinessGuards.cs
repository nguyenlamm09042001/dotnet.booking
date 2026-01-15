using booking.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Infrastructure;

public static class BusinessGuards
{
    public static async Task<bool> EnsureActiveBusiness(ActionExecutingContext ctx, AppDbContext db)
    {
        var userIdStr = ctx.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            RedirectWithSwal(ctx, "error", "Lỗi đăng nhập", "Không xác định được người dùng.");
            return false;
        }

        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null)
        {
            RedirectWithSwal(ctx, "error", "Không tồn tại", "Tài khoản không hợp lệ.");
            return false;
        }

        // Chỉ chặn role business
        if (!string.Equals(u.Role, "business", StringComparison.OrdinalIgnoreCase))
            return true;

        var st = (u.Status ?? "").Trim().ToLower();
        if (st == "active") return true;

        var msg = st switch
        {
            "pending" => "Doanh nghiệp đang chờ duyệt/kích hoạt. Bạn chưa thể tạo dịch vụ hoặc nhân viên.",
            "suspended" => "Doanh nghiệp đang bị tạm khóa. Vui lòng liên hệ quản trị viên.",
            "rejected" => "Doanh nghiệp đã bị từ chối. Vui lòng liên hệ quản trị viên.",
            _ => "Doanh nghiệp chưa đủ điều kiện hoạt động."
        };

        RedirectWithSwal(ctx, "warning", "Chưa được kích hoạt", msg);
        return false;
    }

    private static void RedirectWithSwal(ActionExecutingContext ctx, string type, string title, string message)
    {
        if (ctx.Controller is Controller c)
        {
            c.TempData["SwalType"] = type;
            c.TempData["SwalTitle"] = title;
            c.TempData["SwalMessage"] = message;
            c.TempData["SwalRedirect"] = "/Business/Dashboard"; 
        }

        ctx.Result = new RedirectResult("/Business/Dashboard"); // đổi route nếu dashboard khác
    }
}
