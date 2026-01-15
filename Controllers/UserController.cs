using booking.Data;
using booking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

public class UserController : Controller
{
    private readonly AppDbContext _db;

    // ✅ view paths (đúng /Views/User/...)
    private const string V_HOME_INDEX = "~/Views/User/Home/Index.cshtml";
    private const string V_HOT_INDEX  = "~/Views/User/Hot/Index.cshtml";

    public UserController(AppDbContext db)
    {
        _db = db;
    }

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

      var query = _db.Services
    .AsNoTracking()
    .Include(s => s.BusinessUser)   // ✅ giờ EF sẽ hiểu
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
        sort = (sort ?? "").Trim().ToLowerInvariant();
        query = sort switch
        {
            "rating"     => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount),
            "price_asc"  => query.OrderBy(s => s.Price),
            "price_desc" => query.OrderByDescending(s => s.Price),
            "new"        => query.OrderByDescending(s => s.Id),
            _            => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount)
        };

        var services = await query.ToListAsync();

        // ===== 3) ViewBag =====
        ViewBag.Q = q ?? "";
        ViewBag.Location = location ?? "";
        ViewBag.Category = category ?? ""; // tên danh mục đang chọn
        ViewBag.Sort = sort ?? "";
        ViewBag.CategoryChips = allCats;

        return View(V_HOME_INDEX, services);
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
