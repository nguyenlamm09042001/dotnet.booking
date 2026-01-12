using booking.Data;
using booking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using booking.ViewModels;
using booking.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;


namespace booking.Controllers;

[Authorize(Roles = "business")]
public class BusinessStaffController : Controller
{
    private readonly AppDbContext _db;
    public BusinessStaffController(AppDbContext db) => _db = db;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
{
    var ok = await BusinessGuards.EnsureActiveBusiness(context, _db);
    if (!ok) return;

    await next();
}


    // =========================
    // Swal helper (đúng layout của bé)
    // =========================
    private void SetSwal(string type, string title, string message, string? redirect = null)
    {
        TempData["SwalType"] = type;       // success | error | warning | info | question
        TempData["SwalTitle"] = title;
        TempData["SwalMessage"] = message;

        if (!string.IsNullOrWhiteSpace(redirect))
            TempData["SwalRedirect"] = redirect;
    }

    private int GetBusinessUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out var businessUserId);
        return businessUserId;
    }

    // =========================
    // GET: /BusinessStaff/Index?q=&active=&sort=&page=
    // =========================
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? active, string? sort, int page = 1)
    {
        const int pageSize = 10;
        var businessUserId = GetBusinessUserId();
        if (businessUserId <= 0) return Forbid();

        q = (q ?? "").Trim();
        active = (active ?? "all").Trim().ToLower(); // all|active|inactive
        sort = (sort ?? "new").Trim().ToLower();     // new|name|email|services
        if (page < 1) page = 1;

        // ===== Services của business để assign =====
        var allServices = await _db.Services
            .AsNoTracking()
            .Where(s => s.UserId == businessUserId)
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name)
            .Select(s => new BusinessStaffIndexVm.ServiceOption
            {
                Id = s.Id,
                Name = s.Name,
                IsActive = s.IsActive
            })
            .ToListAsync();

        // ===== Staff base query =====
        var baseQuery =
            from u in _db.Users.AsNoTracking()
            join sp in _db.StaffProfiles.AsNoTracking() on u.Id equals sp.UserId
            where sp.BusinessUserId == businessUserId && u.Role == "staff"
            select new { u, sp };

        if (!string.IsNullOrWhiteSpace(q))
        {
            baseQuery = baseQuery.Where(x =>
    (x.u.FullName ?? "").Contains(q) ||
    x.u.Email.Contains(q));

        }

        if (active == "active")
            baseQuery = baseQuery.Where(x => x.sp.IsActive);
        else if (active == "inactive")
            baseQuery = baseQuery.Where(x => !x.sp.IsActive);

        // ===== Sort (trước khi paging) =====
        baseQuery = sort switch
        {
            "name" => baseQuery.OrderBy(x => x.u.FullName),
            "email" => baseQuery.OrderBy(x => x.u.Email),
            _ => baseQuery.OrderByDescending(x => x.sp.CreatedAt) // new
        };

        // ===== Count + paging =====
        var total = await baseQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        if (totalPages <= 0) totalPages = 1;
        if (page > totalPages) page = totalPages;

        var pageItems = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BusinessStaffIndexVm.StaffRow
            {
                StaffUserId = x.u.Id,
                FullName = x.u.FullName ?? "",
                Email = x.u.Email,
                IsActive = x.sp.IsActive,
                CreatedAt = x.sp.CreatedAt
                // AssignedCount, AssignedServiceIds sẽ fill sau
            })
            .ToListAsync();

        // ===== Load assign services cho staff trong trang hiện tại =====
        var staffIds = pageItems.Select(x => x.StaffUserId).ToList();

        var links = await _db.StaffServices
            .AsNoTracking()
            .Where(ss => staffIds.Contains(ss.StaffUserId))
            .Select(ss => new { ss.StaffUserId, ss.ServiceId })
            .ToListAsync();

        var map = links
            .GroupBy(x => x.StaffUserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ServiceId).Distinct().ToList());

        foreach (var row in pageItems)
        {
            if (map.TryGetValue(row.StaffUserId, out var svcIds))
            {
                row.AssignedServiceIds = svcIds;
                row.AssignedCount = svcIds.Count;
            }
            else
            {
                row.AssignedServiceIds = new List<int>();
                row.AssignedCount = 0;
            }
        }

        // Nếu muốn sort theo "services" thì sort lại ở memory (chỉ trong page)
        if (sort == "services")
            pageItems = pageItems.OrderByDescending(x => x.AssignedCount).ThenBy(x => x.FullName).ToList();

        var vm = new BusinessStaffIndexVm
        {
            Q = q,
            Active = active,
            Sort = sort,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages,
            Items = pageItems,
            AllServices = allServices
        };

        return View("~/Views/Business/Staff/Index.cshtml", vm);
    }

    // =========================
    // POST: /BusinessStaff/Create
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string fullName, string email, string password, int[]? serviceIds)
    {
        var businessUserId = GetBusinessUserId();
        if (businessUserId <= 0) return Forbid();

        fullName = (fullName ?? "").Trim();
        email = (email ?? "").Trim().ToLower();
        password = (password ?? "").Trim();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 2)
        {
            SetSwal("error", "Thiếu thông tin", "Tên nhân viên không hợp lệ.");
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            SetSwal("error", "Thiếu thông tin", "Email không hợp lệ.");
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            SetSwal("error", "Mật khẩu yếu", "Mật khẩu tối thiểu 6 ký tự.");
            return RedirectToAction(nameof(Index));
        }

        var emailExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email);
        if (emailExists)
        {
            SetSwal("error", "Trùng email", "Email này đã tồn tại trong hệ thống.");
            return RedirectToAction(nameof(Index));
        }

        // chỉ cho assign service thuộc business
        serviceIds ??= Array.Empty<int>();
        var validServiceIds = await _db.Services.AsNoTracking()
            .Where(s => s.UserId == businessUserId && serviceIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var staffUser = new User
            {
                Email = email,
                FullName = fullName,
                Role = "staff",
                CreatedAt = DateTime.Now,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };
            _db.Users.Add(staffUser);
            await _db.SaveChangesAsync();

            _db.StaffProfiles.Add(new StaffProfile
            {
                UserId = staffUser.Id,
                BusinessUserId = businessUserId,
                IsActive = true,
                CreatedAt = DateTime.Now
            });

            foreach (var sid in validServiceIds.Distinct())
            {
                _db.StaffServices.Add(new StaffService
                {
                    StaffUserId = staffUser.Id,
                    ServiceId = sid
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            SetSwal("success", "Đã tạo nhân viên", "Tạo nhân viên và gán dịch vụ thành công.");
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await tx.RollbackAsync();
            SetSwal("error", "Lỗi", "Không tạo được nhân viên. Vui lòng thử lại.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // POST: /BusinessStaff/Edit
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int staffUserId, string fullName, string email, string? newPassword)
    {
        var businessUserId = GetBusinessUserId();
        if (businessUserId <= 0) return Forbid();

        fullName = (fullName ?? "").Trim();
        email = (email ?? "").Trim().ToLower();
        newPassword = (newPassword ?? "").Trim();

        // Verify staff belongs to this business
        var sp = await _db.StaffProfiles.FirstOrDefaultAsync(x => x.UserId == staffUserId && x.BusinessUserId == businessUserId);
        if (sp == null)
        {
            SetSwal("error", "Không hợp lệ", "Nhân viên không thuộc doanh nghiệp của bạn.");
            return RedirectToAction(nameof(Index));
        }

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == staffUserId && x.Role == "staff");
        if (u == null)
        {
            SetSwal("error", "Không tìm thấy", "Không tìm thấy nhân viên.");
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 2)
        {
            SetSwal("error", "Thiếu thông tin", "Tên nhân viên không hợp lệ.");
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            SetSwal("error", "Thiếu thông tin", "Email không hợp lệ.");
            return RedirectToAction(nameof(Index));
        }

        var emailExists = await _db.Users.AsNoTracking()
            .AnyAsync(x => x.Email == email && x.Id != staffUserId);
        if (emailExists)
        {
            SetSwal("error", "Trùng email", "Email này đã được dùng bởi tài khoản khác.");
            return RedirectToAction(nameof(Index));
        }

        u.FullName = fullName;
        u.Email = email;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 6)
            {
                SetSwal("error", "Mật khẩu yếu", "Mật khẩu mới tối thiểu 6 ký tự.");
                return RedirectToAction(nameof(Index));
            }
            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        }

        await _db.SaveChangesAsync();
        SetSwal("success", "Đã cập nhật", "Cập nhật thông tin nhân viên thành công.");
        return RedirectToAction(nameof(Index));
    }

    // =========================
    // POST: /BusinessStaff/ToggleActive
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int staffUserId)
    {
        var businessUserId = GetBusinessUserId();
        if (businessUserId <= 0) return Forbid();

        var sp = await _db.StaffProfiles.FirstOrDefaultAsync(x => x.UserId == staffUserId && x.BusinessUserId == businessUserId);
        if (sp == null)
        {
            SetSwal("error", "Không hợp lệ", "Nhân viên không thuộc doanh nghiệp của bạn.");
            return RedirectToAction(nameof(Index));
        }

        sp.IsActive = !sp.IsActive;
        await _db.SaveChangesAsync();

        SetSwal("success", "Đã cập nhật", sp.IsActive ? "Đã kích hoạt nhân viên." : "Đã tạm khóa nhân viên.");
        return RedirectToAction(nameof(Index));
    }

    // =========================
    // POST: /BusinessStaff/Delete
    // - XÓA MỀM: khóa nhân viên + bỏ gán dịch vụ (không xóa user để tránh vỡ dữ liệu booking/review)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int staffUserId)
    {
        var businessUserId = GetBusinessUserId();
        if (businessUserId <= 0) return Forbid();

        var sp = await _db.StaffProfiles.FirstOrDefaultAsync(x => x.UserId == staffUserId && x.BusinessUserId == businessUserId);
        if (sp == null)
        {
            SetSwal("error", "Không hợp lệ", "Nhân viên không thuộc doanh nghiệp của bạn.");
            return RedirectToAction(nameof(Index));
        }

        // Nếu sau này bé muốn chặn xóa khi có booking: check BookingOrders.StaffUserId
        // var hasBooking = await _db.BookingOrders.AnyAsync(b => b.StaffUserId == staffUserId);
        // if (hasBooking) { ... }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            sp.IsActive = false;

            var links = await _db.StaffServices.Where(x => x.StaffUserId == staffUserId).ToListAsync();
            _db.StaffServices.RemoveRange(links);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            SetSwal("success", "Đã xóa", "Đã xóa nhân viên (khóa tài khoản & bỏ gán dịch vụ).");
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await tx.RollbackAsync();
            SetSwal("error", "Lỗi", "Không xóa được nhân viên. Vui lòng thử lại.");
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================
    // POST: /BusinessStaff/AssignServices
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignServices(int staffUserId, int[]? serviceIds)
    {
        var businessUserId = GetBusinessUserId();
        if (businessUserId <= 0) return Forbid();

        // verify staff belongs
        var sp = await _db.StaffProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == staffUserId && x.BusinessUserId == businessUserId);
        if (sp == null)
        {
            SetSwal("error", "Không hợp lệ", "Nhân viên không thuộc doanh nghiệp của bạn.");
            return RedirectToAction(nameof(Index));
        }

        serviceIds ??= Array.Empty<int>();
        var validServiceIds = await _db.Services.AsNoTracking()
            .Where(s => s.UserId == businessUserId && serviceIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var oldLinks = await _db.StaffServices.Where(x => x.StaffUserId == staffUserId).ToListAsync();
            _db.StaffServices.RemoveRange(oldLinks);

            foreach (var sid in validServiceIds.Distinct())
            {
                _db.StaffServices.Add(new StaffService { StaffUserId = staffUserId, ServiceId = sid });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            SetSwal("success", "Đã gán dịch vụ", "Cập nhật dịch vụ cho nhân viên thành công.");
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await tx.RollbackAsync();
            SetSwal("error", "Lỗi", "Không gán được dịch vụ. Vui lòng thử lại.");
            return RedirectToAction(nameof(Index));
        }
    }
}

