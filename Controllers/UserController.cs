using booking.Data;
using booking.Models;
using booking.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

public class UserController : Controller
{
    private readonly AppDbContext _db;

    // ✅ view paths (đúng /Views/User/...)
    private const string V_HOME_INDEX   = "~/Views/User/Home/Index.cshtml";
    private const string V_HOT_INDEX    = "~/Views/User/Hot/Index.cshtml";
    private const string V_BIZ_OVERVIEW = "~/Views/User/Business/Overview.cshtml"; // ✅ thêm

    public UserController(AppDbContext db)
    {
        _db = db;
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    // =========================
    // TRANG KHÁM PHÁ
    // /User/Index?q=&location=&category=&sort=
    // category = tên danh mục (vd: "Tóc") -> lọc theo Services.Category (string)
    // =========================
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? location, string? category, string? sort)
    {
        // ===== 1) Build danh mục chips từ DB (BusinessCategories) nhưng count theo Services.Category =====
        var allCats = await _db.BusinessCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new CategoryChipVm
            {
                Id = c.Id,
                Name = c.Name,
                Count = 0
            })
            .ToListAsync();

        // count theo Services.Category (string)
        var serviceCatCounts = await _db.Services.AsNoTracking()
            .Where(s => s.Category != null && s.Category != "")
            .GroupBy(s => s.Category!)
            .Select(g => new { Name = g.Key, Cnt = g.Count() })
            .ToListAsync();

        // map count theo tên (ignore-case + trim)
        var map = serviceCatCounts
            .GroupBy(x => x.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cnt));

        foreach (var c in allCats)
        {
            var key = (c.Name ?? "").Trim().ToLowerInvariant();
            c.Count = map.TryGetValue(key, out var cnt) ? cnt : 0;
        }

        // ===== 2) Query services =====
        var query = _db.Services
            .AsNoTracking()
            .Include(s => s.BusinessUser)   // ✅ lấy FullName doanh nghiệp
            .AsQueryable();

        // search keyword
        if (!string.IsNullOrWhiteSpace(q))
        {
            var k = q.Trim();
            query = query.Where(s =>
                s.Name.Contains(k) ||
                (s.Description != null && s.Description.Contains(k)) ||
                (s.Location != null && s.Location.Contains(k)) ||
                (s.Category != null && s.Category.Contains(k))
            );
        }

        // filter location
        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim();
            query = query.Where(s => s.Location != null && s.Location.Contains(loc));
        }

        // filter category theo Services.Category (string)
        if (!string.IsNullOrWhiteSpace(category))
        {
            var catName = category.Trim();
            query = query.Where(s => s.Category != null && s.Category == catName);
        }

        // sort
        sort = Norm(sort);
        query = sort switch
        {
            "rating"     => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount),
            "price_asc"  => query.OrderBy(s => s.Price),
            "price_desc" => query.OrderByDescending(s => s.Price),
            "new"        => query.OrderByDescending(s => s.Id),
            _            => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount)
        };

        var services = await query.ToListAsync();

        // ===== 2.5) ✅ Build danh sách Business cards để show ở dashboard user =====
        // Lấy những business xuất hiện trong list services hiện tại (nhẹ + đúng ngữ cảnh)
        var bizIds = services.Select(s => s.UserId).Distinct().ToList();

        var bizCards = await _db.Users.AsNoTracking()
            .Where(u => bizIds.Contains(u.Id) && u.Role == "business")
            .Select(u => new BusinessCardVm
            {
                BusinessUserId = u.Id,
                FullName = u.FullName ?? "",
                Avatar = u.Avatar,
                Status = u.Status,

                ServiceCount = _db.Services.Count(s => s.UserId == u.Id),
                AvgRating = _db.Services
                    .Where(s => s.UserId == u.Id)
                    .Select(s => (decimal?)s.Rating)
                    .Average() ?? 0m,
                TotalReviews = _db.Services
                    .Where(s => s.UserId == u.Id)
                    .Select(s => (int?)s.ReviewCount)
                    .Sum() ?? 0,

                Categories = (
                    from l in _db.BusinessCategoryLinks
                    join c in _db.BusinessCategories on l.CategoryId equals c.Id
                    where l.BusinessUserId == u.Id && c.IsActive
                    select c.Name
                ).Distinct().ToList()
            })
            .OrderByDescending(x => x.AvgRating)
            .ThenByDescending(x => x.TotalReviews)
            .Take(12)
            .ToListAsync();

        var now = DateTime.UtcNow;

var sponsoredBusinesses =
    (from mo in _db.MarketingOrders
     join u in _db.Users on mo.BusinessId equals u.Id
     where mo.Status == "approved"
           && mo.EndAt > now
     select new SponsoredBusinessVm
     {
         BusinessUserId = u.Id,
         FullName = u.FullName ?? "",
         Avatar = u.Avatar,

         ServiceCount = _db.Services
             .Count(s => s.UserId == u.Id),

         AvgRating = _db.Services
             .Where(s => s.UserId == u.Id)
             .Select(s => (double?)s.Rating)
             .Average() ?? 0,

         TotalReviews = _db.Services
             .Where(s => s.UserId == u.Id)
             .Sum(s => s.ReviewCount)
     })
    .Distinct()
    .ToList();

ViewBag.SponsoredBusinesses = sponsoredBusinesses;


        // ===== 3) ViewBag =====
        ViewBag.Q = q ?? "";
        ViewBag.Location = location ?? "";
        ViewBag.Category = category ?? ""; // tên danh mục đang chọn
        ViewBag.Sort = sort ?? "";
        ViewBag.CategoryChips = allCats;

        ViewBag.BusinessCards = bizCards; // ✅ thêm

        return View(V_HOME_INDEX, services);
    }

    // =========================
    // ✅ TRANG TỔNG QUAN DOANH NGHIỆP
    // /User/Business/5
    // =========================
  [HttpGet]
public async Task<IActionResult> Business(int id)
{
    var biz = await _db.Users.AsNoTracking()
        .FirstOrDefaultAsync(u => u.Id == id && u.Role == "business");

    if (biz == null) return NotFound();

    var servicesQuery = _db.Services.AsNoTracking().Where(s => s.UserId == id);

    var totalServices = await servicesQuery.CountAsync();
    var activeServices = await servicesQuery.CountAsync(s => s.IsActive);
var reviewAgg = await (
    from r in _db.ServiceReviews.AsNoTracking()
    join s in _db.Services.AsNoTracking() on r.ServiceId equals s.Id
    where s.UserId == id
    group r by 1 into g
    select new
    {
        Total = g.Count(),
        Avg = g.Average(x => (double)(x.Stars > 0 ? x.Stars : x.Rating))
    }
).FirstOrDefaultAsync();

var totalReviews = reviewAgg?.Total ?? 0;
var avgRating = totalReviews > 0 ? (decimal)(reviewAgg!.Avg) : 0m;


    var categories = await (
        from l in _db.BusinessCategoryLinks.AsNoTracking()
        join c in _db.BusinessCategories.AsNoTracking() on l.CategoryId equals c.Id
        where l.BusinessUserId == id && c.IsActive
        select c.Name
    ).Distinct().ToListAsync();

    // Booking stats: BookingOrders join Services
    var bookingQ =
        from b in _db.BookingOrders.AsNoTracking()
        join s in _db.Services.AsNoTracking() on b.ServiceId equals s.Id
        where s.UserId == id
        select b;

    var totalBookings = await bookingQ.CountAsync();
    var completed = await bookingQ.CountAsync(b => b.Status == "completed");
    var pending = await bookingQ.CountAsync(b => b.Status == "pending");
    var confirmed = await bookingQ.CountAsync(b => b.Status == "confirmed");
    var canceled = await bookingQ.CountAsync(b => b.Status == "canceled");

    var staffCount = await _db.StaffProfiles.AsNoTracking()
        .CountAsync(x => x.BusinessUserId == id && x.IsActive);

    var topServices = await _db.Services.AsNoTracking()
        .Include(s => s.BusinessUser)
        .Where(s => s.UserId == id)
        .OrderByDescending(s => s.Rating)
        .ThenByDescending(s => s.ReviewCount)
        .Take(6)
        .ToListAsync();

    // ✅ Reviews gần nhất + thống kê hôm nay/7 ngày
    var now = DateTime.Now;
    var today = now.Date;
    var from7d = today.AddDays(-7);

    // Query review: ServiceReviews join Services (để biết review thuộc business nào) join Users (để lấy avatar/name)
    var reviewsQ =
        from r in _db.ServiceReviews.AsNoTracking()
        join s in _db.Services.AsNoTracking() on r.ServiceId equals s.Id
        join u in _db.Users.AsNoTracking() on r.UserId equals u.Id
        where s.UserId == id
        select new { r, s, u };

    var newReviewsToday = await reviewsQ.CountAsync(x => x.r.CreatedAt >= today && x.r.CreatedAt < today.AddDays(1));
    var newReviews7d = await reviewsQ.CountAsync(x => x.r.CreatedAt >= from7d);

    var recentReviews = await reviewsQ
        .OrderByDescending(x => x.r.CreatedAt)
        .Take(8)
        .Select(x => new BusinessOverviewVm.RecentReviewItemVm
        {
            ReviewId = x.r.Id,
            Stars = x.r.Stars > 0 ? x.r.Stars : x.r.Rating, // fallback nếu Stars = 0
            Comment = x.r.Comment,
            CreatedAt = x.r.CreatedAt,

            ServiceId = x.s.Id,
            ServiceName = x.s.Name,

            ReviewerUserId = x.u.Id,
            ReviewerName = x.u.FullName ?? "",
            ReviewerAvatar = x.u.Avatar
        })
        .ToListAsync();

    // ✅ Review theo từng dịch vụ (accordion)
var topServiceIds = topServices.Select(x => x.Id).ToList();

var reviewsByService = await (
    from r in _db.ServiceReviews.AsNoTracking()
    join s in _db.Services.AsNoTracking() on r.ServiceId equals s.Id
    join u in _db.Users.AsNoTracking() on r.UserId equals u.Id
    where s.UserId == id && topServiceIds.Contains(s.Id)
    orderby r.CreatedAt descending
    select new
    {
        ServiceId = s.Id,
        ServiceName = s.Name,
        ServiceThumbnail = s.Thumbnail,
        ServiceRating = s.Rating,
        ServiceReviewCount = s.ReviewCount,

        ReviewId = r.Id,
        Stars = r.Stars > 0 ? r.Stars : r.Rating,
        r.Comment,
        r.CreatedAt,

        ReviewerId = u.Id,
        ReviewerName = u.FullName,
        ReviewerAvatar = u.Avatar
    }
).ToListAsync();


// ✅ Stats review thật theo từng service (đếm từ ServiceReviews)
var reviewStats = await (
    from r in _db.ServiceReviews.AsNoTracking()
    join s in _db.Services.AsNoTracking() on r.ServiceId equals s.Id
    where s.UserId == id
    group r by r.ServiceId into g
    select new
    {
        ServiceId = g.Key,
        Cnt = g.Count(),
        Avg = g.Average(x => (double)(x.Stars > 0 ? x.Stars : x.Rating))
    }
).ToListAsync();

var reviewCntMap = reviewStats.ToDictionary(x => x.ServiceId, x => x.Cnt);
var reviewAvgMap = reviewStats.ToDictionary(x => x.ServiceId, x => x.Avg);


var groups = topServices.Select(s =>
{
    var realCnt = reviewCntMap.TryGetValue(s.Id, out var c) ? c : 0;
    var realAvg = reviewAvgMap.TryGetValue(s.Id, out var a) ? (decimal)a : 0m;

    return new BusinessOverviewVm.ServiceReviewGroupVm
    {
        ServiceId = s.Id,
        ServiceName = s.Name,
        ServiceThumbnail = s.Thumbnail,

        // ✅ dùng số thật
        ReviewCount = realCnt,
        Rating = realCnt > 0 ? realAvg : 0m,

        Reviews = reviewsByService
            .Where(x => x.ServiceId == s.Id)
            .Take(5)
            .Select(x => new BusinessOverviewVm.RecentReviewItemVm
            {
                ReviewId = x.ReviewId,
                Stars = x.Stars,
                Comment = x.Comment,
                CreatedAt = x.CreatedAt,
                ServiceId = s.Id,
                ServiceName = s.Name,
                ReviewerUserId = x.ReviewerId,
                ReviewerName = x.ReviewerName,
                ReviewerAvatar = x.ReviewerAvatar
            })
            .ToList()
    };
}).ToList();




    var vm = new BusinessOverviewVm
    {
        BusinessUserId = biz.Id,
        FullName = biz.FullName ??  "",
        Avatar = biz.Avatar,
        Status = biz.Status,

        Categories = categories,

        TotalServices = totalServices,
        ActiveServices = activeServices,
        AvgRating = avgRating,
        TotalReviews = totalReviews,

        TotalBookings = totalBookings,
        CompletedBookings = completed,
        PendingBookings = pending,
        ConfirmedBookings = confirmed,
        CanceledBookings = canceled,

        StaffCount = staffCount,

        NewReviewsToday = newReviewsToday,
        NewReviews7d = newReviews7d,
        RecentReviews = recentReviews,

        TopServices = topServices,

        ServiceReviewGroups = groups

    };

    return View(V_BIZ_OVERVIEW, vm);
}

    // =========================
    // 🔥 TRANG ĐANG HOT RIÊNG
    // /User/Hot?q=...&tag=...
    // =========================
    [HttpGet]
    public async Task<IActionResult> Hot(string? q, string? tag, string? location, string? category, string? sort)
    {
        var keyword = MapHotTag(tag) ?? q;

        var query = _db.Services
            .AsNoTracking()
            .Include(s => s.BusinessUser) // ✅ thêm để lấy FullName doanh nghiệp
            //.Where(s => s.IsActive)     // ✅ tuỳ chọn
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(s =>
                s.Name.Contains(k) ||
                (s.Description != null && s.Description.Contains(k)) ||
                (s.Location != null && s.Location.Contains(k)) ||
                (s.Category != null && s.Category.Contains(k))
            );
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim();
            query = query.Where(s => s.Location != null && s.Location.Contains(loc));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            query = query.Where(s => s.Category != null && s.Category == cat);
        }

        sort = string.IsNullOrWhiteSpace(sort) ? "rating" : sort.Trim().ToLowerInvariant();

        query = sort switch
        {
            "rating"     => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount),
            "price_asc"  => query.OrderBy(s => s.Price),
            "price_desc" => query.OrderByDescending(s => s.Price),
            "new"        => query.OrderByDescending(s => s.Id),
            _            => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount)
        };

        var services = await query.ToListAsync();

        ViewBag.Q = keyword ?? "";
        ViewBag.Location = location ?? "";
        ViewBag.Category = category ?? "";
        ViewBag.Sort = sort ?? "rating";
        ViewBag.HotTitle = !string.IsNullOrWhiteSpace(keyword)
            ? $"Đang hot: {keyword}"
            : "Dịch vụ đang hot";

        return View(V_HOT_INDEX, services);
    }

    // =========================
    // MAP TAG → KEYWORD
    // =========================
    private string? MapHotTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        return tag.ToLower() switch
        {
            "cat-toc"   => "Cắt tóc",
            "rua-xe"    => "Rửa xe",
            "khach-san" => "Khách sạn",
            "massage"   => "Massage",
            "thu-cung"  => "Thú cưng",
            _ => null
        };
    }

    // VM nhỏ cho chips danh mục
    public class CategoryChipVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}
