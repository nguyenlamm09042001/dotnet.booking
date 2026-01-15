using booking.Models;

namespace booking.ViewModels;

public class ServiceDetailsVm
{
    public Service Service { get; set; } = default!;
    public List<ReviewItemVm> Reviews { get; set; } = new();

    public double AvgRating { get; set; }
    public int ReviewCount { get; set; }

        public string? Avatar { get; set; }

        public string? OwnerFullName { get; set; }
public string? OwnerAvatar { get; set; }

}
