namespace booking.Models;

public class BusinessCategoryLink
{
    public int BusinessUserId { get; set; }  // FK -> Users.Id
    public User BusinessUser { get; set; } = null!;

    public int CategoryId { get; set; }      // FK -> BusinessCategories.Id
    public BusinessCategory Category { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
