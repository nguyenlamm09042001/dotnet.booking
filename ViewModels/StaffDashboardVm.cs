namespace booking.ViewModels;

public class StaffDashboardVm
{
    public string StaffName { get; set; } = "";
    public bool IsActive { get; set; }

    public int TodayTotal { get; set; }
    public int TodayPending { get; set; }
    public int TodayConfirmed { get; set; }

    public int Upcoming7d { get; set; }
    public int AssignedServicesCount { get; set; }

    public List<BookingRow> TodayBookings { get; set; } = new();
    public List<BookingRow> UpcomingBookings { get; set; } = new();

    public class BookingRow
    {
        public int Id { get; set; }
        public string ServiceName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public string Status { get; set; } = "pending";
        public string? Note { get; set; }
    }
}
