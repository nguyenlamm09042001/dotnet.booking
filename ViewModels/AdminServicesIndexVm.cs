using booking.Models;

namespace booking.ViewModels;

public class AdminServicesIndexVm
{
    public List<Service> Items { get; set; } = new();

    // paging
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    public int FromItem => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int ToItem => Math.Min(Page * PageSize, TotalItems);

    // filters
    public string? Q { get; set; }
    public string? Status { get; set; }   // all | active | inactive
    public string? Cat { get; set; }
    public string? Sort { get; set; }     // newest | oldest | name_asc | name_desc | price_asc | price_desc
    public List<string> Categories { get; set; } = new();

    // stats
    public int TotalServices { get; set; }
    public int ActiveServices { get; set; }
    public int InactiveServices { get; set; }
}
