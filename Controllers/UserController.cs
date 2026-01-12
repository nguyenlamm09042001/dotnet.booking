using booking.Data;
using booking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace booking.Controllers;

public class UserController : Controller
{
    private readonly AppDbContext _db;

    public UserController(AppDbContext db)
    {
        _db = db;
    }

    // =========================
    // TRANG KHÁM PHÁ
    // /User/Index
    // =========================
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? location, string? category, string? sort)
    {
        var query = _db.Services.AsNoTracking();

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

        query = sort switch
        {
            "rating" => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount),
            "price_asc" => query.OrderBy(s => s.Price),
            "price_desc" => query.OrderByDescending(s => s.Price),
            "new" => query.OrderByDescending(s => s.Id),
            _ => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount)
        };

        var services = await query.ToListAsync();

        ViewBag.Q = q ?? "";
        ViewBag.Location = location ?? "";
        ViewBag.Category = category ?? "";
        ViewBag.Sort = sort ?? "";

        return View("Home/Index", services);
    }

    // =========================
    // 🔥 TRANG ĐANG HOT RIÊNG
    // /User/Hot/Index?q=Cắt tóc
    // /User/Hot/Index?tag=cat-toc
    // =========================
    [HttpGet]
    public async Task<IActionResult> Hot(string? q, string? tag, string? location, string? category, string? sort)
    {
        // tag ưu tiên hơn q
        var keyword = MapHotTag(tag) ?? q;

        var query = _db.Services.AsNoTracking();

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

        // mặc định trang hot sort theo rating
        sort = string.IsNullOrWhiteSpace(sort) ? "rating" : sort;

        query = sort switch
        {
            "rating" => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount),
            "price_asc" => query.OrderBy(s => s.Price),
            "price_desc" => query.OrderByDescending(s => s.Price),
            "new" => query.OrderByDescending(s => s.Id),
            _ => query.OrderByDescending(s => s.Rating).ThenByDescending(s => s.ReviewCount)
        };

        var services = await query.ToListAsync();

        ViewBag.Q = keyword ?? "";
        ViewBag.Location = location ?? "";
        ViewBag.Category = category ?? "";
        ViewBag.Sort = sort ?? "rating";
        ViewBag.HotTitle = !string.IsNullOrWhiteSpace(keyword)
            ? $"Đang hot: {keyword}"
            : "Dịch vụ đang hot";

        return View("Hot/Index", services);
    }

    // =========================
    // MAP TAG → KEYWORD
    // =========================
    private string? MapHotTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        return tag.ToLower() switch
        {
            "cat-toc" => "Cắt tóc",
            "rua-xe" => "Rửa xe",
            "khach-san" => "Khách sạn",
            "massage" => "Massage",
            "thu-cung" => "Thú cưng",
            _ => null
        };
    }
}
