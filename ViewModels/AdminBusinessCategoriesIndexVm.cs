using System;
using System.Collections.Generic;
using booking.Models;

namespace booking.ViewModels;

public class AdminBusinessCategoriesIndexVm
{
    // filters
    public string? Q { get; set; }
    public string Status { get; set; } = "all";   // all | active | inactive
    public string Sort { get; set; } = "newest";  // newest | oldest | name_asc | name_desc

    // stats
    public int TotalCategories { get; set; }
    public int ActiveCategories { get; set; }
    public int InactiveCategories { get; set; }

    // paging
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    public int FromItem => TotalItems == 0 ? 0 : ((Page - 1) * PageSize + 1);
    public int ToItem => TotalItems == 0 ? 0 : Math.Min(Page * PageSize, TotalItems);

    // data
    public List<BusinessCategoryRow> Items { get; set; } = new();

    // row vm
    public class BusinessCategoryRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // optional: dùng để show số business đang gán category (nếu controller có join count)
        public int LinkedBusinesses { get; set; }
    }
}
