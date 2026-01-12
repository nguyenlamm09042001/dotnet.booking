namespace booking.ViewModels;

public class BusinessStaffIndexVm
{
    public string? Q { get; set; }
    public string? Active { get; set; } // all|active|inactive
    public string? Sort { get; set; }   // new|name|email|services

    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }

    public List<StaffRow> Items { get; set; } = new();
    public List<ServiceOption> AllServices { get; set; } = new();

    public class StaffRow
    {
        public int StaffUserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public int AssignedCount { get; set; }
        public List<int> AssignedServiceIds { get; set; } = new();
    }

    public class ServiceOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
