namespace booking.ViewModels;

public class AdminDashboardVm
{
    public int TotalUsers { get; set; }
    public int NewUsers7d { get; set; }
    public int TotalBusinesses { get; set; }
    public int PendingBusinesses { get; set; }
    public int ActiveBusinesses { get; set; }
    public int SystemAlerts { get; set; }

    public List<UserRow> RecentUsers { get; set; } = new();

    // ✅ preview doanh nghiệp pending để show trên dashboard
    public List<BusinessRow> PendingBusinessesPreview { get; set; } = new();

    public class UserRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class BusinessRow
    {
        public int Id { get; set; }
        public string BusinessName { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "";
        public int RiskLevel { get; set; }
    }
}
