namespace booking.ViewModels;

public class BusinessDashboardVm
{

    public int PendingBookings { get; set; }   
public int PendingSwapRequests { get; set; } 
public int PendingReviews { get; set; }   
    public int RevenueMonth { get; set; }             // doanh thu tháng (VND)
    public int ServicesActive { get; set; }           // số dịch vụ đang active
    public int BookingsToday { get; set; }            // lịch hẹn hôm nay
    public int NewReviews7d { get; set; }             // review mới 7 ngày
    public int CancelRate { get; set; }               // % hủy (0-100)
    public double AvgRating { get; set; }             // điểm TB

    public List<RecentBookingRow> RecentBookings { get; set; } = new();
    public List<RecentReviewRow> RecentReviews { get; set; } = new();

    public class RecentBookingRow
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string ServiceName { get; set; } = "";
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public string Status { get; set; } = "";
    }

    public class RecentReviewRow
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = "";
        public int Rating { get; set; }               // 1..5
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
