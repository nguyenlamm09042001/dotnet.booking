namespace booking.ViewModels;

public class AdminServiceCategoriesIndexVm
{
    // FILTERS
    public string? Q { get; set; }
    public string? Status { get; set; }
    public string? Sort { get; set; }

    // STATS
    public int TotalCategories { get; set; }
    public int ActiveCategories { get; set; }
    public int InactiveCategories { get; set; }

    // PAGINATION
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    public int FromItem => TotalItems == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int ToItem => TotalItems == 0 ? 0 : Math.Min(Page * PageSize, TotalItems);

    // LIST
    public List<Row> Items { get; set; } = new();

    public class Row
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
