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

    // ✅ push noti cho tất cả admin
    private async Task PushNotiToAllAdminsAsync(string title, string message, string type = "warning", string? linkUrl = null)
    {
        var adminIds = await _db.Users
            .AsNoTracking()
            .Where(u => (u.Role ?? "").ToLower() == "admin")
            .Select(u => u.Id)
            .ToListAsync();

        if (adminIds.Count == 0) return;

        foreach (var adminId in adminIds)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = adminId,
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    // accountType: "user" | "business"
    private static string MapAccountTypeToRole(string? accountType)
    {
        return (accountType ?? "").Trim().ToLowerInvariant() switch
        {
            "business" => "business",
            "user" => "customer",
            _ => "customer"
        };
    }

    // dashboard sau login theo role
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
    // REGISTER (STEP 1)
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

        // business mặc định pending, user thường active
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

        // ✅ FLOW MỚI:
        // Nếu là business -> bắt buộc qua bước chọn danh mục trước
        if ((role ?? "").Trim().ToLowerInvariant() == "business")
        {
            return RedirectToAction("ChooseCategories", new { id = user.Id });
        }

        // user thường -> xong là login
        SetSwal(
            "success",
            "Đăng ký thành công",
            "Tài khoản đã tạo. Bây giờ bạn có thể đăng nhập.",
            Url.Action("Login", "Account")
        );

        return RedirectToAction("Login");
    }

    // =========================
    // CHOOSE BUSINESS CATEGORIES (STEP 2 - BẮT BUỘC)
    // =========================
    [HttpGet]
    public async Task<IActionResult> ChooseCategories(int id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (user == null)
        {
            SetSwal("error", "Không tìm thấy", "Tài khoản không tồn tại.");
            return RedirectToAction("Register");
        }

        var role = (user.Role ?? "").Trim().ToLowerInvariant();
        if (role != "business")
        {
            SetSwal("warning", "Không hợp lệ", "Chỉ tài khoản doanh nghiệp mới chọn danh mục.");
            return RedirectToAction("Login");
        }

        var categories = await _db.BusinessCategories.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CategoryItemVm
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                IsChecked = false
            })
            .ToListAsync();

        if (categories.Count == 0)
        {
            SetSwal("error", "Chưa có danh mục", "Hệ thống chưa có danh mục doanh nghiệp. Vui lòng seed dữ liệu.");
            return RedirectToAction("Register");
        }

        var vm = new ChooseBusinessCategoriesVm
        {
            BusinessUserId = id,
            Categories = categories
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChooseCategories(ChooseBusinessCategoriesVm vm)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == vm.BusinessUserId);
        if (user == null)
        {
            SetSwal("error", "Không tìm thấy", "Tài khoản không tồn tại.");
            return RedirectToAction("Register");
        }

        var role = (user.Role ?? "").Trim().ToLowerInvariant();
        if (role != "business")
        {
            SetSwal("warning", "Không hợp lệ", "Chỉ tài khoản doanh nghiệp mới chọn danh mục.");
            return RedirectToAction("Login");
        }

        if (vm.CategoryIds == null || vm.CategoryIds.Count == 0)
        {
            ModelState.AddModelError("CategoryIds", "Vui lòng chọn ít nhất 1 danh mục.");
        }

        var validIds = await _db.BusinessCategories.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();

        var picked = (vm.CategoryIds ?? new()).Distinct().ToList();
        if (picked.Any(cid => !validIds.Contains(cid)))
        {
            ModelState.AddModelError("CategoryIds", "Có danh mục không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            vm.Categories = await _db.BusinessCategories.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new CategoryItemVm
                {
                    Id = x.Id,
                    Name = x.Name,
                    Code = x.Code,
                    IsChecked = picked.Contains(x.Id)
                })
                .ToListAsync();

            return View(vm);
        }

        // clear cũ (phòng trường hợp quay lại)
        var oldLinks = await _db.BusinessCategoryLinks
            .Where(x => x.BusinessUserId == vm.BusinessUserId)
            .ToListAsync();

        if (oldLinks.Count > 0)
            _db.BusinessCategoryLinks.RemoveRange(oldLinks);

        // add mới
        var links = picked.Select(cid => new BusinessCategoryLink
        {
            BusinessUserId = vm.BusinessUserId,
            CategoryId = cid,
            CreatedAt = DateTime.UtcNow
        });

        _db.BusinessCategoryLinks.AddRange(links);
        await _db.SaveChangesAsync();

        // ✅ SAU KHI CHỌN DANH MỤC XONG -> MỚI BẮN NOTI CHO ADMIN
        await PushNotiToAllAdminsAsync(
            "Doanh nghiệp mới chờ duyệt",
            $"Có doanh nghiệp mới đăng ký: {user.FullName} ({user.Email}).",
            "warning",
            "/AdminBusinesses/Index"
        );

        SetSwal(
            "success",
            "Hoàn tất đăng ký doanh nghiệp",
            "Bạn đã chọn danh mục kinh doanh. Tài khoản đang chờ admin duyệt/kích hoạt.",
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
        var status = string.IsNullOrWhiteSpace(user.Status) ? "active" : user.Status.Trim().ToLowerInvariant();

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
        // ✅ Chặn customer bị suspended
if (role == "customer" && status == "suspended")
{
    SetSwal("error", "Tài khoản bị tạm đình chỉ", "Tài khoản của bạn đang bị tạm đình chỉ. Vui lòng liên hệ quản trị viên.");
    return View(vm);
}


        // ✅ Chặn staff bị khóa
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

        SetSwal("success", "Đăng nhập thành công", "Chào mừng bạn quay lại!", redirect);
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
