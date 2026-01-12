using booking.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace booking.Controllers;

[Authorize(Roles = "business")]
public class BusinessReviewsController : Controller
{
    private readonly AppDbContext _db;
    public BusinessReviewsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public IActionResult Index()
        => View("~/Views/Business/Reviews/Index.cshtml");
}
