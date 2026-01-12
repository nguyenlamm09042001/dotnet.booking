namespace booking.ViewModels;

public class AdminBusinessesIndexVm
{
    public string? Query { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int FromItem { get; set; }
    public int ToItem { get; set; }

    public List<BusinessRowVm> Items { get; set; } = new();
}

public class BusinessRowVm
{
    public int Id { get; set; }
    public string? Name { get; set; }

    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Status { get; set; } // hiện tại DB chưa có => UI demo

    public int ServiceCount { get; set; }
    public int BookingCount { get; set; }
}
