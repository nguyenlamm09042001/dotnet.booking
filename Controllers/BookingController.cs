using booking.Data;
using booking.Models;
using booking.ViewModels;
using booking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly AppDbContext _db;
    private readonly NotificationService _noti;

    public BookingController(AppDbContext db, NotificationService noti)
    {
        _db = db;
        _noti = noti;
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
    // GET /Booking/AvailableSlots?serviceId=2&date=2026-01-15
    // Trả JSON slot theo ngày (dùng luồng B: còn staff rảnh)
    // =========================
    [HttpGet]
    public async Task<IActionResult> AvailableSlots(int serviceId, string date)
    {
        if (serviceId <= 0) return BadRequest(new { message = "serviceId invalid" });

        if (!DateOnly.TryParse(date, out var d))
            return BadRequest(new { message = "date invalid (yyyy-MM-dd)" });

        // service tồn tại + đang active?
        var s = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == serviceId);
        if (s == null) return NotFound(new { message = "service not found" });

        if (!s.IsActive)
        {
            // tuỳ ý: trả toàn booked hoặc trả rỗng
            return Ok(new List<object>());
        }

        var slots = await BuildSlotsAsync(serviceId, d, SLOT_START, SLOT_END, SLOT_STEP_MINUTES);

        if (slots.Count == 0)
        {
            var options = BuildTimeOptions(SLOT_START, SLOT_END, SLOT_STEP_MINUTES);
            slots = options.Select(t => new TimeSlotVm
            {
                Value = t,
                IsBooked = false,
                Capacity = 0,
                Remaining = 0,
                IsPast = false
            }).ToList();
        }

        var payload = slots.Select(x => new
        {
            value = x.Value,
            isBooked = x.IsBooked,
            capacity = x.Capacity,
            remaining = x.Remaining,
            isPast = x.IsPast
        }).ToList();

        return Ok(payload);
    }

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
            return RedirectToAction("Index", "Services");
        }

        var date = DateOnly.FromDateTime(DateTime.Today);

        var slots = await BuildSlotsAsync(serviceId, date, SLOT_START, SLOT_END, SLOT_STEP_MINUTES);

        if (slots.Count == 0)
        {
            var options = BuildTimeOptions(SLOT_START, SLOT_END, SLOT_STEP_MINUTES);
            slots = options.Select(t => new TimeSlotVm
            {
                Value = t,
                IsBooked = false,
                Capacity = 0,
                Remaining = 0,
                IsPast = false
            }).ToList();
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
            vm.TimeSlots = options.Select(t => new TimeSlotVm
            {
                Value = t,
                IsBooked = false,
                Capacity = 0,
                Remaining = 0,
                IsPast = false
            }).ToList();
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

        // =========================
        // CHẶN NGÀY QUÁ KHỨ / GIỜ ĐÃ QUA (server-side)
        // =========================
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 1) ngày trong quá khứ
        if (vm.Date < today)
        {
            SetSwal("warning", "Không hợp lệ", "Bạn không thể chọn ngày trong quá khứ. Vui lòng chọn ngày khác.");
            ModelState.AddModelError(nameof(vm.Date), "Ngày đã qua.");
            return View(VIEW_BOOKING_CREATE, vm);
        }

        // 2) nếu chọn hôm nay thì giờ phải >= hiện tại
        if (vm.Date == today)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);

            // Rule: slot bắt đầu trước "now" là không hợp lệ
            if (t < now)
            {
                SetSwal("warning", "Không hợp lệ", "Giờ bạn chọn đã qua. Vui lòng chọn giờ khác.");
                ModelState.AddModelError(nameof(vm.Time), "Giờ đã qua.");
                return View(VIEW_BOOKING_CREATE, vm);
            }
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

        // (OPTIONAL) chặn 1 user đặt 2 lần cùng giờ cùng service
        var existedSameTime = await _db.BookingOrders.AsNoTracking().AnyAsync(b =>
            b.UserId == userId &&
            b.ServiceId == vm.ServiceId &&
            b.Date == vm.Date &&
            b.Time == t &&
            !new[] { "canceled", "cancelled" }.Contains((b.Status ?? "").Trim().ToLower())
        );

        if (existedSameTime)
        {
            SetSwal("warning", "Thông báo", "Bạn đã đặt khung giờ này rồi. Vui lòng chọn giờ khác.");
            ModelState.AddModelError(nameof(vm.Time), "Bạn đã đặt khung giờ này rồi.");
            return View(VIEW_BOOKING_CREATE, vm);
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
            Status = ST_PENDING,            // staff tự confirm
            CreatedAt = DateTime.UtcNow
        };

        _db.BookingOrders.Add(booking);
        await _db.SaveChangesAsync();

        // =========================
        // NOTIFICATION: booking mới
        // =========================
        try
        {
            // notify business (owner của service)
            await _noti.CreateAsync(
                userId: businessUserId,
                title: "Có lịch hẹn mới",
                message: $"#{booking.Id} • {booking.CustomerName} • {booking.Date:dd/MM/yyyy} {booking.Time:HH\\:mm}",
                type: "booking_new",
                linkUrl: $"/BusinessBookings/Details/{booking.Id}"
            );

            // notify staff được auto-assign
            await _noti.CreateAsync(
                userId: booking.StaffUserId!.Value,
                title: "Bạn có lịch mới",
                message: $"#{booking.Id} • {booking.CustomerName} • {booking.Date:dd/MM/yyyy} {booking.Time:HH\\:mm}",
                type: "booking_assigned",
                linkUrl: $"/Staff/BookingDetail?id={booking.Id}"
            );
        }
        catch
        {
            // không chặn luồng đặt lịch nếu noti lỗi
        }

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
    // POST: /Booking/Cancel  (User huỷ trong 10 phút)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId) || userId <= 0)
        {
            SetSwal("error", "Không hợp lệ", "Bạn chưa đăng nhập hợp lệ.");
            return RedirectToAction(nameof(Index));
        }

        var booking = await _db.BookingOrders
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (booking == null)
        {
            SetSwal("error", "Không tìm thấy", "Booking không tồn tại hoặc bạn không có quyền.");
            return RedirectToAction(nameof(Index));
        }

        var stLower = NormStatus(booking.Status);
        if (stLower != ST_PENDING)
        {
            SetSwal("warning", "Không thể huỷ", "Chỉ có thể huỷ khi booking đang ở trạng thái 'Chờ xác nhận'.");
            return RedirectToAction(nameof(Index));
        }

        var createdUtc = booking.CreatedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(booking.CreatedAt, DateTimeKind.Utc)
            : booking.CreatedAt.ToUniversalTime();

        if (DateTime.UtcNow > createdUtc.AddMinutes(10))
        {
            SetSwal("warning", "Hết hạn huỷ", "Booking chỉ được huỷ trong vòng 10 phút kể từ lúc đặt.");
            return RedirectToAction(nameof(Index));
        }

        booking.Status = ST_CANCELED;
        await _db.SaveChangesAsync();

        try
        {
            var businessUserId = booking.Service?.UserId
                ?? await _db.Services.AsNoTracking()
                    .Where(s => s.Id == booking.ServiceId)
                    .Select(s => s.UserId)
                    .FirstOrDefaultAsync();

            if (businessUserId > 0)
            {
                await _noti.CreateAsync(
                    userId: businessUserId,
                    title: "Khách đã huỷ lịch",
                    message: $"#{booking.Id} • {booking.CustomerName} • {booking.Date:dd/MM/yyyy} {booking.Time:HH\\:mm}",
                    type: "booking_canceled",
                    linkUrl: $"/BusinessBookings/Details/{booking.Id}"
                );
            }

            if (booking.StaffUserId.HasValue)
            {
                await _noti.CreateAsync(
                    userId: booking.StaffUserId.Value,
                    title: "Lịch đã bị huỷ",
                    message: $"#{booking.Id} • {booking.CustomerName} • {booking.Date:dd/MM/yyyy} {booking.Time:HH\\:mm}",
                    type: "booking_canceled",
                    linkUrl: $"/Staff/BookingDetail?id={booking.Id}"
                );
            }
        }
        catch
        {
            // không chặn cancel nếu noti lỗi
        }

        SetSwal("success", "Đã huỷ", "Booking của bạn đã được huỷ thành công.");
        return RedirectToAction(nameof(Index));
    }

    // =========================
    // Helpers (slot builder) - LUỒNG B (capacity/remaining)
    // =========================
    private async Task<List<TimeSlotVm>> BuildSlotsAsync(
        int serviceId, DateOnly date, string start, string end, int stepMinutes)
    {
        var options = BuildTimeOptions(start, end, stepMinutes);
        if (options.Count == 0) return new();

        // lấy service => biết business + duration
        var s = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == serviceId);
        if (s == null)
        {
            return options.Select(t => new TimeSlotVm
            {
                Value = t,
                IsBooked = true,
                Capacity = 0,
                Remaining = 0,
                IsPast = false
            }).ToList();
        }

        var businessUserId = s.UserId;
        var dur = (s.DurationMinutes > 0) ? s.DurationMinutes : stepMinutes;

        // staff eligible
        var eligible = await GetEligibleStaffIdsAsync(businessUserId, serviceId);
        if (eligible.Count == 0)
        {
            return options.Select(t => new TimeSlotVm
            {
                Value = t,
                IsBooked = true,
                Capacity = 0,
                Remaining = 0,
                IsPast = false
            }).ToList();
        }

        var cap = eligible.Count;

        var result = new List<TimeSlotVm>();

        // ===== đánh dấu slot đã qua nếu chọn HÔM NAY =====
        var today = DateOnly.FromDateTime(DateTime.Today);
        var nowMin = ToMinutes(TimeOnly.FromDateTime(DateTime.Now));

        foreach (var opt in options)
        {
            TimeOnly.TryParse(opt, out var tt);

            var reqStart = ToMinutes(tt);
            var reqEnd = reqStart + Math.Max(1, dur);

            // past-time chỉ xét khi date == today
            var isPast = (date == today) && (reqStart < nowMin);

            var busy = await GetBusyStaffIdsAsync(date, reqStart, reqEnd, eligible);
            var remaining = cap - busy.Count;

            // nếu đã qua giờ thì coi như không chọn được (UI sẽ disable)
            if (isPast)
            {
                remaining = 0;
            }

            result.Add(new TimeSlotVm
            {
                Value = opt,
                Capacity = cap,
                Remaining = remaining,
                IsPast = isPast,
                IsBooked = remaining <= 0
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

      var busy = await (
    from b in _db.BookingOrders.AsNoTracking()
    join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
    where b.Date == date
          && b.StaffUserId != null
          && eligibleStaffIds.Contains(b.StaffUserId.Value)
          && !new[] { "canceled", "cancelled" }.Contains((b.Status ?? "").Trim().ToLower())
    let bStart = (b.Time.Hour * 60 + b.Time.Minute)
    let dur = (s.DurationMinutes > 0 ? s.DurationMinutes : SLOT_STEP_MINUTES)
    let bEnd = bStart + dur
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
        let dur = (s.DurationMinutes > 0 ? s.DurationMinutes : SLOT_STEP_MINUTES)
        let bEnd = bStart + dur
        where bStart < reqEnd && bEnd > reqStart
        select b.Id
    ).AnyAsync();

    if (!stillBusy) return sid;
}

        return null;
    }
}
