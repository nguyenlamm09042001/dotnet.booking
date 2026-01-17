using System.ComponentModel.DataAnnotations;

namespace booking.Models;

public class MarketingOrder
{
    public int Id { get; set; }
    public int BusinessId { get; set; }

    public int Days { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    [MaxLength(32)]
    public string Code { get; set; } = "";            // mã đơn

    [MaxLength(32)]
    public string PaymentCode { get; set; } = "";     // ✅ mã CK (nội dung chuyển khoản)

    [MaxLength(20)]
    public string Status { get; set; } = "draft";     // ✅ draft/pending/approved/rejected

    public DateTime? PaidAt { get; set; }             // ✅ lúc xác nhận đã CK
    public DateTime? ApprovedAt { get; set; }

    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    [MaxLength(255)]
    public string? PaymentNote { get; set; }          // user nhập nội dung đã CK / ghi chú

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
