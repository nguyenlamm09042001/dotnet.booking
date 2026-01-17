using booking.Data;
using booking.Models;
using booking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace booking.Controllers;

[Authorize(Roles = "business")]
public class BusinessMarketingController : Controller
{
    private readonly AppDbContext _db;

    private const string VIEW_CREATE = "~/Views/Business/Marketing/Create.cshtml";
    private const string VIEW_INDEX  = "~/Views/Business/Marketing/Index.cshtml";
    private const string VIEW_PAY    = "~/Views/Business/Marketing/Pay.cshtml";

    // Bank config (demo)
    private const string BANK_BIN = "970407";    // Techcombank
    private const string BANK_ACC = "999321";    // STK
    private const string BANK_NAME = "Nguyễn Lam";

    // Price config
    private const decimal UNIT_PRICE = 15000m;

    public BusinessMarketingController(AppDbContext db)
    {
        _db = db;
    }

    private int CurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var uid = CurrentUserId();
        if (uid == 0) return Unauthorized();

        var orders = await _db.MarketingOrders
            .AsNoTracking()
            .Where(x => x.BusinessId == uid)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync();

        return View(VIEW_INDEX, orders);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var vm = new MarketingCreateVm
        {
            StartAt = DateTime.Today,
            Days = 2,
            UnitPrice = UNIT_PRICE
        };
        return View(VIEW_CREATE, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MarketingCreateVm vm)
    {
        if (vm.Days < 1)
            ModelState.AddModelError(nameof(vm.Days), "Số ngày phải >= 1.");

        if (!ModelState.IsValid)
        {
            // giữ giá hiển thị consistent
            vm.UnitPrice = UNIT_PRICE;
            return View(VIEW_CREATE, vm);
        }

        var uid = CurrentUserId();
        if (uid == 0) return Unauthorized();

        var start = vm.StartAt.Date;
        var end = start.AddDays(vm.Days); // EndAt exclusive (tới đầu ngày kế)

        var code = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(); // mã đơn
        var payCode = $"ADS-{code}";                                     // mã CK

        var order = new MarketingOrder
        {
            BusinessId = uid,

            StartAt = start,
            EndAt = end,

            Days = vm.Days,
            UnitPrice = UNIT_PRICE,
            Amount = vm.Days * UNIT_PRICE,

            Code = code,
            PaymentCode = payCode,

            Status = "draft",
            CreatedAt = DateTime.UtcNow
        };

        _db.MarketingOrders.Add(order);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Pay), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Pay(int id)
    {
        var uid = CurrentUserId();
        if (uid == 0) return Unauthorized();

        var order = await _db.MarketingOrders
            .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == uid);

        if (order == null) return NotFound();

        // chỉ cho pay khi draft
        if (!string.Equals(order.Status, "draft", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Index));

        BindPayViewBags(order);

        // VM chỉ cần Id (+ PaymentNote nếu có)
        var vm = new MarketingPayVm { Id = order.Id };
        return View(VIEW_PAY, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(MarketingPayVm vm)
    {
        var uid = CurrentUserId();
        if (uid == 0) return Unauthorized();

        var order = await _db.MarketingOrders
            .FirstOrDefaultAsync(x => x.Id == vm.Id && x.BusinessId == uid);

        if (order == null) return NotFound();

        if (!string.Equals(order.Status, "draft", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Index));

        // Nếu VM có required field (ví dụ PaymentNote) mà invalid => vẫn phải render lại QR
        if (!ModelState.IsValid)
        {
            BindPayViewBags(order);
            return View(VIEW_PAY, vm);
        }

        // ✅ xác nhận đã CK -> pending chờ admin
        order.PaymentNote = (vm.PaymentNote ?? "").Trim();
        order.PaidAt = DateTime.UtcNow;
        order.Status = "pending";

        await _db.SaveChangesAsync();

        TempData["SwalType"] = "success";
        TempData["SwalTitle"] = "Đã ghi nhận chuyển khoản";
        TempData["SwalMessage"] = $"Đơn {order.Code} đã vào hàng chờ duyệt.";

        return RedirectToAction(nameof(Index));
    }

    // ===== Helpers =====
    private void BindPayViewBags(MarketingOrder order)
    {
        ViewBag.PayCode = order.PaymentCode;
        ViewBag.Amount = order.Amount;
        ViewBag.Days = order.Days;
        ViewBag.StartAt = order.StartAt?.ToString("dd/MM/yyyy") ?? "—";

        var content = $"ADS {order.Code}";
        ViewBag.TransferContent = content;

        // QR VietQR
        ViewBag.QrUrl =
            $"https://img.vietqr.io/image/{BANK_BIN}-{BANK_ACC}-compact2.png" +
            $"?amount={order.Amount}&addInfo={Uri.EscapeDataString(content)}";

        ViewBag.BankName = "Techcombank";
        ViewBag.BankAccountNo = BANK_ACC;
        ViewBag.BankAccountName = BANK_NAME;
    }
}
