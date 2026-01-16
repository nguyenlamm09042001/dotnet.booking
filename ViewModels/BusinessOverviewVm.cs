using booking.Models;

namespace booking.ViewModels;

public class BusinessOverviewVm
{
    public int BusinessUserId { get; set; }
    public string FullName { get; set; } = "";
    public string? Avatar { get; set; }
    public string Status { get; set; } = "pending";

    public List<string> Categories { get; set; } = new();

    public int TotalServices { get; set; }
    public int ActiveServices { get; set; }
    public decimal AvgRating { get; set; }
    public int TotalReviews { get; set; }

    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CanceledBookings { get; set; }

    public int StaffCount { get; set; }

    public int NewReviewsToday { get; set; }
    public int NewReviews7d { get; set; }

    public List<Service> TopServices { get; set; } = new();
    public List<RecentReviewItemVm> RecentReviews { get; set; } = new();

    public class RecentReviewItemVm
    {
        public int ReviewId { get; set; }
        public int Stars { get; set; }              // lấy Stars (hoặc fallback Rating)
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }

        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = "";

        public int ReviewerUserId { get; set; }
        public string ReviewerName { get; set; } = "";
        public string? ReviewerAvatar { get; set; }
    }

    public List<ServiceReviewGroupVm> ServiceReviewGroups { get; set; } = new();

public class ServiceReviewGroupVm
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = "";
    public string? ServiceThumbnail { get; set; }

    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }

    public List<RecentReviewItemVm> Reviews { get; set; } = new();
}

}
