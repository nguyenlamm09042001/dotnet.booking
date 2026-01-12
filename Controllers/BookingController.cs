using booking.Data;
using booking.Models;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly AppDbContext _db;

    public BookingController(AppDbContext db)
    {
        _db = db;
    }

    // =========================
    // VIEW PATHS (đúng theo /Views/User/...)
    // =========================
    private const string VIEW_BOOKING_INDEX = "~/Views/User/Booking/Index.cshtml";
    private const string VIEW_BOOKING_CREATE = "~/Views/User/Booking/Create.cshtml";

    // =========================
    // STATUS (chuẩn hoá lowercase)
    // =========================
    private const string ST_PENDING = "pending";
    private const string ST_CONFIRMED = "confirmed";
    private const string ST_COMPLETED = "completed";
    private const string ST_CANCELED = "canceled";

    private static string NormStatus(string? s) => (s ?? "").Trim().ToLower();

    // =========================
    // Swal helper (đúng theo _Layout.cshtml của bé)
    // =========================
    private void SetSwal(string type, string title, string message, string? redirect = null)
    {
        TempData["SwalType"] = type;       // success | error | warning | info | question
        TempData["SwalTitle"] = title;
        TempData["SwalMessage"] = message;

        if (!string.IsNullOrWhiteSpace(redirect))
            TempData["SwalRedirect"] = redirect;
    }

    // =========================
    // CONFIG SLOT TIME
    // =========================
    private const string SLOT_START = "09:00";
    private const string SLOT_END = "20:00";
    private const int SLOT_STEP_MINUTES = 30;

    // =========================
    // GET /Booking/Create?serviceId=2
    // =========================
    [HttpGet]
    public async Task<IActionResult> Create(int serviceId)
    {
        var s = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == serviceId);

        if (s == null) return NotFound();

        if (!s.IsActive)
        {
            SetSwal("warning", "Thông báo", "Dịch vụ đang tạm đóng, chưa thể đặt lịch.");
            return RedirectToAction("Index", "Services"); // /Views/User/Services/Index.cshtml
        }

        var date = DateOnly.FromDateTime(DateTime.Today);

        var slots = await BuildSlotsAsync(serviceId, date, SLOT_START, SLOT_END, SLOT_STEP_MINUTES);

        if (slots.Count == 0)
        {
            var options = BuildTimeOptions(SLOT_START, SLOT_END, SLOT_STEP_MINUTES);
            slots = options.Select(t => new TimeSlotVm { Value = t, IsBooked = false }).ToList();
        }

        var firstFree = slots.FirstOrDefault(x => !x.IsBooked)?.Value ?? slots.First().Value;

        var vm = new BookingCreateVm
        {
            ServiceId = s.Id,
            ServiceName = s.Name,
            Price = s.Price,
            DurationMinutes = s.DurationMinutes,
            Location = s.Location,

            Date = date,
            Time = firstFree,

            TimeSlots = slots,
            TimeOptions = slots.Select(x => x.Value).ToList()
        };

        return View(VIEW_BOOKING_CREATE, vm);
    }

    // =========================
    // POST /Booking/Create  (LUỒNG B: AUTO-ASSIGN STAFF)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingCreateVm vm)
    {
        vm.TimeSlots = await BuildSlotsAsync(vm.ServiceId, vm.Date, SLOT_START, SLOT_END, SLOT_STEP_MINUTES);

        if (vm.TimeSlots.Count == 0)
        {
            var options = BuildTimeOptions(SLOT_START, SLOT_END, SLOT_STEP_MINUTES);
            vm.TimeSlots = options.Select(t => new TimeSlotVm { Value = t, IsBooked = false }).ToList();
        }

        vm.TimeOptions = vm.TimeSlots.Select(x => x.Value).ToList();

        var s = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == vm.ServiceId);

        if (s == null)
        {
            SetSwal("error", "Lỗi", "Dịch vụ không tồn tại.");
            return RedirectToAction(nameof(Create), new { serviceId = vm.ServiceId });
        }

        vm.ServiceName = s.Name;
        vm.Price = s.Price;
        vm.DurationMinutes = s.DurationMinutes;
        vm.Location = s.Location;

        if (!s.IsActive)
        {
            SetSwal("warning", "Thông báo", "Dịch vụ đang tạm đóng, chưa thể đặt lịch.");
            ModelState.AddModelError("", "Dịch vụ đang tạm đóng.");
            return View(VIEW_BOOKING_CREATE, vm);
        }

        if (!ModelState.IsValid)
            return View(VIEW_BOOKING_CREATE, vm);

        if (!TimeOnly.TryParse(vm.Time, out var t))
        {
            SetSwal("error", "Lỗi", "Giờ không hợp lệ.");
            ModelState.AddModelError(nameof(vm.Time), "Giờ không hợp lệ.");
            return View(VIEW_BOOKING_CREATE, vm);
        }

        var pickedSlot = vm.TimeSlots.FirstOrDefault(x => x.Value == vm.Time);
        if (pickedSlot == null)
        {
            SetSwal("error", "Lỗi", "Khung giờ không tồn tại.");
            ModelState.AddModelError(nameof(vm.Time), "Khung giờ không tồn tại.");
            return View(VIEW_BOOKING_CREATE, vm);
        }

        // Với luồng B, IsBooked nghĩa là "hết staff rảnh"
        if (pickedSlot.IsBooked)
        {
            SetSwal("warning", "Thông báo", "Khung giờ này hiện không còn nhân viên trống. Chọn giờ khác nha.");
            ModelState.AddModelError(nameof(vm.Time), "Khung giờ đã hết nhân viên trống.");
            return View(VIEW_BOOKING_CREATE, vm);
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            SetSwal("error", "Lỗi", "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.", "/Account/Login");
            return RedirectToAction("Login", "Account");
        }

        // ===== LUỒNG B: AUTO ASSIGN STAFF =====
        var businessUserId = s.UserId;
        var dur = (s.DurationMinutes > 0) ? s.DurationMinutes : SLOT_STEP_MINUTES;


        var staffId = await PickStaffAsync(businessUserId, vm.ServiceId, vm.Date, t, dur);

        // Nếu ngay lúc bấm đặt mà không còn staff rảnh => báo user chọn giờ khác
        if (staffId == null)
        {
            SetSwal("warning", "Thông báo",
                $"Giờ {vm.Time} ngày {vm.Date:dd/MM/yyyy} hiện không còn nhân viên trống. Chọn giờ khác nha.");

            ModelState.AddModelError(nameof(vm.Time), "Khung giờ này hiện không còn nhân viên trống.");
            vm.TimeSlots = await BuildSlotsAsync(vm.ServiceId, vm.Date, SLOT_START, SLOT_END, SLOT_STEP_MINUTES);
            vm.TimeOptions = vm.TimeSlots.Select(x => x.Value).ToList();
            return View(VIEW_BOOKING_CREATE, vm);
        }

        var booking = new BookingOrder
        {
            UserId = userId,
            ServiceId = vm.ServiceId,
            StaffUserId = staffId.Value, // ✅ auto assign
            CustomerName = vm.CustomerName.Trim(),
            Phone = vm.Phone.Trim(),
            Date = vm.Date,
            Time = t,
            Note = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim(),
            Status = ST_PENDING,            // giữ pending để staff tự confirm (bé muốn auto-confirm thì đổi ST_CONFIRMED)
            CreatedAt = DateTime.UtcNow
        };

        _db.BookingOrders.Add(booking);
        await _db.SaveChangesAsync();

        SetSwal("success", "Thành công", $"Đặt lịch thành công! Mã #{booking.Id}");
        return RedirectToAction(nameof(Index));
    }

    // =========================
    // GET /Booking  => "Lịch của tôi" + filter status
    // =========================
    [HttpGet]
    public async Task<IActionResult> Index(string status = "all")
    {
        var statusNorm = NormStatus(status);
        if (string.IsNullOrWhiteSpace(statusNorm)) statusNorm = "all";
        ViewBag.Status = statusNorm;

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return RedirectToAction("Login", "Account");

        var query = _db.BookingOrders
            .AsNoTracking()
            .Include(b => b.Service)
            .Where(b => b.UserId == userId);

        if (!statusNorm.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => (b.Status ?? "").Trim().ToLower() == statusNorm);
        }

        var list = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var now = DateTime.Now;
        var bookingIds = list.Select(x => x.Id).ToList();

        var reviewedSet = await _db.ServiceReviews
            .AsNoTracking()
            .Where(r => bookingIds.Contains(r.BookingOrderId))
            .Select(r => r.BookingOrderId)
            .ToHashSetAsync();

        var vm = list.Select(b =>
        {
            var bookingTime = b.Date.ToDateTime(b.Time);
            var isTimePassed = bookingTime <= now;

            var st = NormStatus(b.Status);

            var isCompletedLogic =
                st == ST_COMPLETED
                || (st == ST_CONFIRMED && isTimePassed);

            return new BookingIndexItemVm
            {
                Id = b.Id,
                ServiceId = b.ServiceId,
                ServiceName = b.Service?.Name ?? "Dịch vụ",

                Status = st,
                CreatedAt = b.CreatedAt,

                CustomerName = b.CustomerName,
                Phone = b.Phone,

                Date = b.Date,
                Time = b.Time,

                Note = b.Note,

                IsTimePassed = isTimePassed,
                IsCompletedLogic = isCompletedLogic,
                HasReview = reviewedSet.Contains(b.Id)
            };
        }).ToList();

        return View(VIEW_BOOKING_INDEX, vm);
    }

    // =========================
    // POST /Booking/CreateReview
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReview(ReviewCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            SetSwal("error", "Lỗi", "Dữ liệu đánh giá không hợp lệ.");
            return RedirectToAction(nameof(Index));
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            SetSwal("error", "Lỗi", "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.", "/Account/Login");
            return RedirectToAction("Login", "Account");
        }

        var booking = await _db.BookingOrders
            .FirstOrDefaultAsync(x => x.Id == vm.BookingOrderId && x.UserId == userId);

        if (booking == null)
        {
            SetSwal("error", "Lỗi", "Không tìm thấy lịch hẹn.");
            return RedirectToAction(nameof(Index));
        }

        var now = DateTime.Now;
        var bookingTime = booking.Date.ToDateTime(booking.Time);
        var isTimePassed = bookingTime <= now;

        var st = NormStatus(booking.Status);
        var isCompletedLogic = st == ST_COMPLETED || (st == ST_CONFIRMED && isTimePassed);

        if (!isCompletedLogic)
        {
            SetSwal("warning", "Thông báo", "Chỉ có thể đánh giá sau khi lịch hoàn tất.");
            return RedirectToAction(nameof(Index));
        }

        var existed = await _db.ServiceReviews
            .AsNoTracking()
            .AnyAsync(x => x.BookingOrderId == vm.BookingOrderId);

        if (existed)
        {
            SetSwal("info", "Thông báo", "Lịch này đã được đánh giá rồi.");
            return RedirectToAction(nameof(Index));
        }

        var review = new ServiceReview
        {
            BookingOrderId = vm.BookingOrderId,
            ServiceId = booking.ServiceId,
            UserId = userId,
            Rating = vm.Rating,
            Comment = vm.Comment?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.ServiceReviews.Add(review);
        await _db.SaveChangesAsync();

        SetSwal("success", "Thành công", "Gửi đánh giá thành công. Cảm ơn bạn nha!");
        return RedirectToAction(nameof(Index));
    }

    // =========================
    // Helpers (slot builder) - LUỒNG B
    // =========================
    private async Task<List<TimeSlotVm>> BuildSlotsAsync(
        int serviceId, DateOnly date, string start, string end, int stepMinutes)
    {
        var options = BuildTimeOptions(start, end, stepMinutes);
        if (options.Count == 0) return new();

        // lấy service => biết business + duration
        var s = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == serviceId);
        if (s == null)
            return options.Select(t => new TimeSlotVm { Value = t, IsBooked = true }).ToList();

        var businessUserId = s.UserId;
        var dur = (s.DurationMinutes > 0) ? s.DurationMinutes : stepMinutes;

        // staff eligible
        var eligible = await GetEligibleStaffIdsAsync(businessUserId, serviceId);
        if (eligible.Count == 0)
            return options.Select(t => new TimeSlotVm { Value = t, IsBooked = true }).ToList();

        var result = new List<TimeSlotVm>();

        // MVP: loop từng slot và check còn staff rảnh hay không
        foreach (var opt in options)
        {
            TimeOnly.TryParse(opt, out var tt);
            var reqStart = ToMinutes(tt);
            var reqEnd = reqStart + Math.Max(1, dur);

            var busy = await GetBusyStaffIdsAsync(date, reqStart, reqEnd, eligible);
            var freeCount = eligible.Count - busy.Count;

            result.Add(new TimeSlotVm
            {
                Value = opt,
                IsBooked = freeCount <= 0
            });
        }

        return result;
    }

    private static List<string> BuildTimeOptions(string start, string end, int stepMinutes)
    {
        var result = new List<string>();
        if (!TimeOnly.TryParse(start, out var tStart)) return result;
        if (!TimeOnly.TryParse(end, out var tEnd)) return result;
        if (stepMinutes <= 0) return result;

        for (var t = tStart; t <= tEnd; t = t.AddMinutes(stepMinutes))
            result.Add(t.ToString("HH:mm"));

        return result;
    }

    // =========================
    // AUTO ASSIGN HELPERS (Luồng B)
    // =========================
    private async Task<List<int>> GetEligibleStaffIdsAsync(int businessUserId, int serviceId)
    {
        // staff thuộc business + active + có nhận service
        var ids = await (
            from sp in _db.StaffProfiles.AsNoTracking()
            join ss in _db.StaffServices.AsNoTracking()
                on sp.UserId equals ss.StaffUserId
            where sp.BusinessUserId == businessUserId
                  && sp.IsActive == true
                  && ss.ServiceId == serviceId
            select sp.UserId
        ).Distinct().ToListAsync();

        return ids;
    }

    private static int ToMinutes(TimeOnly t) => t.Hour * 60 + t.Minute;

    private async Task<List<int>> GetBusyStaffIdsAsync(
        DateOnly date,
        int reqStartMin,
        int reqEndMin,
        List<int> eligibleStaffIds)
    {
        if (eligibleStaffIds.Count == 0) return new List<int>();

        // Busy nếu overlap khoảng thời gian (tính theo duration của service từng booking)
        var busy = await (
            from b in _db.BookingOrders.AsNoTracking()
            join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
            where b.Date == date
                  && b.StaffUserId != null
                  && eligibleStaffIds.Contains(b.StaffUserId.Value)
                  && !new[] { "canceled", "cancelled" }.Contains((b.Status ?? "").Trim().ToLower())
            let bStart = (b.Time.Hour * 60 + b.Time.Minute)
            let bEnd = (b.Time.Hour * 60 + b.Time.Minute) + (s.DurationMinutes > 0 ? s.DurationMinutes : 0)

            where bStart < reqEndMin && bEnd > reqStartMin
            select b.StaffUserId!.Value
        ).Distinct().ToListAsync();

        return busy;
    }

    private async Task<int?> PickStaffAsync(int businessUserId, int serviceId, DateOnly date, TimeOnly startTime, int durationMinutes)
    {
        var eligible = await GetEligibleStaffIdsAsync(businessUserId, serviceId);
        if (eligible.Count == 0) return null;

        var reqStart = ToMinutes(startTime);
        var reqEnd = reqStart + Math.Max(1, durationMinutes);

        var busy = await GetBusyStaffIdsAsync(date, reqStart, reqEnd, eligible);
        var free = eligible.Except(busy).ToList();
        if (free.Count == 0) return null;

        // Fair: ít booking nhất trong ngày
        var load = await _db.BookingOrders.AsNoTracking()
            .Where(b => b.Date == date
                        && b.StaffUserId != null
                        && free.Contains(b.StaffUserId.Value)
                        && !new[] { "canceled", "cancelled" }.Contains((b.Status ?? "").Trim().ToLower()))
            .GroupBy(b => b.StaffUserId!.Value)
            .Select(g => new { StaffId = g.Key, Cnt = g.Count() })
            .ToListAsync();

        int CntOf(int sid) => load.FirstOrDefault(x => x.StaffId == sid)?.Cnt ?? 0;

        // Giảm race condition: thử từng staff theo thứ tự, check lại còn rảnh không
        foreach (var sid in free.OrderBy(sid => CntOf(sid)).ThenBy(sid => sid))
        {
            var stillBusy = await (
                from b in _db.BookingOrders.AsNoTracking()
                join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
                where b.Date == date
                      && b.StaffUserId == sid
                      && !new[] { "canceled", "cancelled" }.Contains((b.Status ?? "").Trim().ToLower())
                let bStart = (b.Time.Hour * 60 + b.Time.Minute)
                let bEnd = (b.Time.Hour * 60 + b.Time.Minute) + (s.DurationMinutes > 0 ? s.DurationMinutes : 0)

                where bStart < reqEnd && bEnd > reqStart
                select b.Id
            ).AnyAsync();

            if (!stillBusy) return sid;
        }

        return null;
    }
}
