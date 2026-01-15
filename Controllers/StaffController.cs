using booking.Data;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize(Roles = "staff")]
public class StaffController : Controller
{
    private readonly AppDbContext _db;
    public StaffController(AppDbContext db) => _db = db;

    private int GetUserId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(s, out var id) ? id : 0;
    }


    private void SetSwal(string type, string title, string message, string? redirect = null)
    {
        TempData["SwalType"] = type;
        TempData["SwalTitle"] = title;
        TempData["SwalMessage"] = message;
        if (!string.IsNullOrWhiteSpace(redirect))
            TempData["SwalRedirect"] = redirect;
    }

    // ✅ Helper: push notification
    private async Task PushNotiAsync(int userId, string title, string message, string type = "info", string? link = null)
    {
        if (userId <= 0) return;

        _db.Notifications.Add(new booking.Models.Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            LinkUrl = link,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    // GET: /Staff
    public async Task<IActionResult> Index()
    {
        var staffUserId = GetUserId();
        if (staffUserId <= 0) return Forbid();

        var sp = await _db.StaffProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == staffUserId);

        if (sp == null)
        {
            SetSwal("error", "Chưa có StaffProfile", "Tài khoản staff này chưa được gán vào doanh nghiệp.");
            return View("~/Views/Staff/Home/Index.cshtml", new StaffDashboardVm());
        }

        var staff = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == staffUserId);
        var staffName = staff?.FullName ?? "Nhân viên";

        var assignedServiceIds = await _db.StaffServices
            .AsNoTracking()
            .Where(x => x.StaffUserId == staffUserId)
            .Select(x => x.ServiceId)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = today;
        var end7 = today.AddDays(7);

        var baseQuery =
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where s.UserId == sp.BusinessUserId
                  && b.StaffUserId == staffUserId
            select new { b, s };

        var todayList = await baseQuery
            .Where(x => x.b.Date == today)
            .OrderBy(x => x.b.Time)
            .Select(x => new StaffDashboardVm.BookingRow
            {
                Id = x.b.Id,
                ServiceName = x.s.Name,
                CustomerName = x.b.CustomerName,
                Phone = x.b.Phone,
                Date = x.b.Date,
                Time = x.b.Time,
                Status = x.b.Status,
                Note = x.b.Note
            })
            .ToListAsync();

        var upcomingList = await baseQuery
            .Where(x => x.b.Date > start && x.b.Date <= end7)
            .OrderBy(x => x.b.Date).ThenBy(x => x.b.Time)
            .Take(10)
            .Select(x => new StaffDashboardVm.BookingRow
            {
                Id = x.b.Id,
                ServiceName = x.s.Name,
                CustomerName = x.b.CustomerName,
                Phone = x.b.Phone,
                Date = x.b.Date,
                Time = x.b.Time,
                Status = x.b.Status,
                Note = x.b.Note
            })
            .ToListAsync();

        int CountStatus(IEnumerable<StaffDashboardVm.BookingRow> list, string st)
            => list.Count(x => (x.Status ?? "").ToLower() == st);

        var vm = new StaffDashboardVm
        {
            StaffName = staffName,
            IsActive = sp.IsActive,
            AssignedServicesCount = assignedServiceIds.Count,

            TodayTotal = todayList.Count,
            TodayPending = CountStatus(todayList, "pending"),
            TodayConfirmed = CountStatus(todayList, "confirmed"),

            Upcoming7d = await baseQuery.CountAsync(x => x.b.Date > start && x.b.Date <= end7),

            TodayBookings = todayList,
            UpcomingBookings = upcomingList
        };

        return View("~/Views/Staff/Home/Index.cshtml", vm);
    }

    // ✅ NEW: GET /Staff/BookingDetail?id=123  (modal fetch)
    [HttpGet]
    public async Task<IActionResult> BookingDetail(int id)
    {
        var staffUserId = GetUserId();
        if (staffUserId <= 0) return Forbid();

        var sp = await _db.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == staffUserId);
        if (sp == null) return Forbid();

        var data = await (
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where b.Id == id
                  && b.StaffUserId == staffUserId
                  && s.UserId == sp.BusinessUserId
            select new StaffDetailBookingVm
            {
                Id = b.Id,
                Status = b.Status,
                CustomerName = b.CustomerName,
                Phone = b.Phone,
                Note = b.Note,

                ServiceId = s.Id,
                ServiceName = s.Name,
                Category = s.Category,
                Location = s.Location,

                Date = b.Date,
                Time = b.Time
            }
        ).FirstOrDefaultAsync();

        if (data == null)
            return NotFound(new { message = "Không tìm thấy booking hoặc bạn không có quyền xem." });

        return Json(data);
    }

    // POST: /Staff/UpdateBookingStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBookingStatus(int id, string toStatus)
    {
        var staffUserId = GetUserId();
        if (staffUserId <= 0) return Forbid();

        toStatus = (toStatus ?? "").Trim().ToLower();
        var allowed = new[] { "confirmed", "completed", "canceled" };
        if (!allowed.Contains(toStatus))
        {
            SetSwal("error", "Trạng thái không hợp lệ", "Vui lòng thử lại.");
            return RedirectToAction("Index");
        }

        var sp = await _db.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == staffUserId);
        if (sp == null) return Forbid();

        var booking = await _db.BookingOrders.FirstOrDefaultAsync(x => x.Id == id && x.StaffUserId == staffUserId);
        if (booking == null)
        {
            SetSwal("error", "Không tìm thấy booking", "Booking không tồn tại hoặc không thuộc về bạn.");
            return RedirectToAction("Index");
        }

        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == booking.ServiceId);
        if (service == null || service.UserId != sp.BusinessUserId) return Forbid();

        var cur = (booking.Status ?? "").ToLower();
        if (toStatus == "completed" && cur != "confirmed")
        {
            SetSwal("warning", "Chưa thể hoàn thành", "Booking phải ở trạng thái 'Đã xác nhận' trước.");
            return RedirectToAction("Index");
        }

        booking.Status = toStatus;
        await _db.SaveChangesAsync();

        // ✅ push notification cho user đặt
        string title, msg, type;
        string link = "/Booking/Index";

        switch (toStatus)
        {
            case "confirmed":
                title = "Booking đã được xác nhận";
                msg = $"Lịch #{booking.Id} đã được nhân viên xác nhận.";
                type = "success";
                link = "/Booking/Index?status=confirmed";
                break;

            case "completed":
                title = "Booking đã hoàn thành";
                msg = $"Lịch #{booking.Id} đã hoàn thành. Bạn có thể vào 'Lịch của tôi' để đánh giá.";
                type = "success";
                link = "/Booking/Index?status=completed";
                break;

            case "canceled":
                title = "Booking đã bị huỷ";
                msg = $"Lịch #{booking.Id} đã bị huỷ bởi nhân viên.";
                type = "warning";
                link = "/Booking/Index?status=cancelled";
                break;

            default:
                title = "Cập nhật booking";
                msg = $"Lịch #{booking.Id} đã cập nhật trạng thái: {toStatus}.";
                type = "info";
                break;
        }

        await PushNotiAsync(booking.UserId, title, msg, type, link);

        string label = toStatus switch
        {
            "confirmed" => "Đã xác nhận",
            "completed" => "Hoàn thành",
            "canceled" => "Đã hủy",
            _ => toStatus
        };

        SetSwal("success", "Cập nhật thành công", $"Booking #{id}: {label}");
        return RedirectToAction("Index");
    }

    // POST: /Staff/ToggleActive
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive()
    {
        var staffUserId = GetUserId();
        if (staffUserId <= 0) return Forbid();

        var sp = await _db.StaffProfiles.FirstOrDefaultAsync(x => x.UserId == staffUserId);
        if (sp == null) return Forbid();

        sp.IsActive = !sp.IsActive;
        await _db.SaveChangesAsync();

        SetSwal("success", "Đã cập nhật", sp.IsActive ? "Bạn đang ở trạng thái HOẠT ĐỘNG." : "Bạn đang ở trạng thái TẠM NGHỈ.");
        return RedirectToAction("Index");
    }

    // POST: /Staff/ApproveBooking (pending -> confirmed)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBooking(int id)
    {
        var staffUserId = GetUserId();
        if (staffUserId <= 0) return Forbid();

        var booking = await _db.BookingOrders
            .FirstOrDefaultAsync(b => b.Id == id && b.StaffUserId == staffUserId);

        if (booking == null)
        {
            SetSwal("error", "Lỗi", "Booking không tồn tại hoặc không thuộc về bạn.");
            return RedirectToAction("Index");
        }

        if ((booking.Status ?? "").ToLower() != "pending")
        {
            SetSwal("warning", "Không thể xác nhận", "Booking này không còn ở trạng thái chờ.");
            return RedirectToAction("Index");
        }

        booking.Status = "confirmed";
        await _db.SaveChangesAsync();

        // ✅ notification cho user đặt
        await PushNotiAsync(
            booking.UserId,
            "Booking đã được xác nhận",
            $"Lịch #{booking.Id} đã được nhân viên xác nhận.",
            "success",
            "/Booking/Index?status=confirmed"
        );

        SetSwal("success", "Đã xác nhận", $"Bạn đã nhận booking #{booking.Id}");
        return RedirectToAction("Index");
    }
}
