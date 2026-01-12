using booking.Data;
using booking.Models;
using booking.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

    // ===== Helpers: set TempData for Swal =====
    private void SetSwal(string type, string title, string message, string? redirectUrl = null)
    {
        TempData["SwalType"] = type;           // success | error | warning | info | question
        TempData["SwalTitle"] = title;
        TempData["SwalMessage"] = message;
        if (!string.IsNullOrWhiteSpace(redirectUrl))
            TempData["SwalRedirect"] = redirectUrl;
    }

    // Chuẩn hoá role từ lựa chọn Register
    // accountType: "user" | "business" (từ UI)
    private static string MapAccountTypeToRole(string? accountType)
    {
        return (accountType ?? "").Trim().ToLowerInvariant() switch
        {
            "business" => "business",
            "user" => "customer",
            _ => "customer"
        };
    }

    // Quyết định dashboard sau login theo role
    private string GetDashboardUrl(string? role)
    {
        var r = (role ?? "customer").Trim().ToLowerInvariant();

        return r switch
        {
            "business" => Url.Action("Index", "Business") ?? "/Business",
            "admin"    => Url.Action("Index", "Admin") ?? "/Admin",
            "staff"    => Url.Action("Index", "Staff") ?? "/Staff",
            _          => Url.Action("Index", "User") ?? "/User"
        };
    }

    // =========================
    // REGISTER
    // =========================
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterVm { AccountType = "user" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid)
        {
            SetSwal("warning", "Thiếu thông tin", "Vui lòng kiểm tra lại các trường đã nhập.");
            return View(vm);
        }

        var email = (vm.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(nameof(vm.Email), "Email is required.");
            SetSwal("warning", "Email trống", "Vui lòng nhập email.");
            return View(vm);
        }

        var exists = await _db.Users.AnyAsync(x => x.Email == email);
        if (exists)
        {
            ModelState.AddModelError(nameof(vm.Email), "Email already exists.");
            SetSwal("error", "Email đã tồn tại", "Vui lòng dùng email khác hoặc đăng nhập.");
            return View(vm);
        }

        var role = MapAccountTypeToRole(vm.AccountType);

        var fullName = (vm.FullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            ModelState.AddModelError(nameof(vm.FullName), "Full name is required.");
            SetSwal("warning", "Thiếu họ tên", "Vui lòng nhập họ và tên.");
            return View(vm);
        }

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
            FullName = fullName,
            Role = role ?? "customer",
            CreatedAt = DateTime.UtcNow
        };

        // =========================
        // ✅ E) Khi tạo business account: set Status = pending
        // =========================
        if ((role ?? "").Trim().ToLowerInvariant() == "business")
        {
            user.Status = "pending";       // pending|active|suspended|rejected
            user.BusinessApprovedAt = null;
            user.BusinessApprovedBy = null;
            user.BusinessRiskLevel = 0;
            user.BusinessVerifiedAt = null;
        }
        else
        {
            user.Status = "active";
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        SetSwal(
            "success",
            "Đăng ký thành công",
            (role == "business")
                ? "Tài khoản doanh nghiệp đã tạo và đang chờ duyệt/kích hoạt. Bạn có thể đăng nhập sau khi được duyệt."
                : "Tài khoản đã tạo. Bây giờ bé có thể đăng nhập.",
            Url.Action("Login", "Account")
        );

        return RedirectToAction("Login");
    }

    // =========================
    // LOGIN
    // =========================
    [HttpGet]
    public IActionResult Login()
    {
        if (User?.Identity?.IsAuthenticated ?? false)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var redirect = GetDashboardUrl(role);
            return Redirect(redirect);
        }

        return View(new LoginVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid)
        {
            SetSwal("warning", "Thiếu thông tin", "Vui lòng nhập email và mật khẩu.");
            return View(vm);
        }

        await Task.Delay(300);

        var email = (vm.Email ?? "").Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Invalid email or password.");
            SetSwal("error", "Đăng nhập thất bại", "Email hoặc mật khẩu không đúng.");
            return View(vm);
        }

        var role = string.IsNullOrWhiteSpace(user.Role) ? "customer" : user.Role.Trim().ToLowerInvariant();

        var status = string.IsNullOrWhiteSpace(user.Status)
            ? "active"
            : user.Status.Trim().ToLowerInvariant();

        // ✅ Chặn business chưa active
        if (role == "business" && status != "active")
        {
            var msg = status switch
            {
                "pending"   => "Tài khoản doanh nghiệp đang chờ duyệt/kích hoạt. Vui lòng đợi quản trị viên phê duyệt.",
                "suspended" => "Tài khoản doanh nghiệp đang bị tạm khóa. Vui lòng liên hệ quản trị viên.",
                "rejected"  => "Tài khoản doanh nghiệp đã bị từ chối. Vui lòng liên hệ quản trị viên.",
                _           => "Tài khoản doanh nghiệp chưa đủ điều kiện hoạt động."
            };

            SetSwal("warning", "Chưa được kích hoạt", msg);
            return View(vm);
        }

        // ✅ Chặn staff bị khóa (StaffProfiles.IsActive = false)
        if (role == "staff")
        {
            var sp = await _db.StaffProfiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            if (sp == null || !sp.IsActive)
            {
                SetSwal("error", "Tài khoản bị khóa", "Nhân viên đang bị tạm khóa hoặc không hợp lệ. Vui lòng liên hệ doanh nghiệp.");
                return View(vm);
            }
        }

        // ✅ Claims cookie
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, role),
            new Claim("FullName", user.FullName ?? ""),
            new Claim("Status", status)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            }
        );

        var redirect = GetDashboardUrl(role);

        SetSwal("success", "Đăng nhập thành công", "Chào mừng bé quay lại!", redirect);
        return Redirect(redirect);
    }

    // =========================
    // LOGOUT
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        TempData.Clear();

        SetSwal(
            "success",
            "Đã đăng xuất",
            "Đã xoá phiên đăng nhập. Vui lòng đăng nhập lại.",
            Url.Action("Login", "Account")
        );

        return RedirectToAction("Login", "Account");
    }
}
