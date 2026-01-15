using System.ComponentModel.DataAnnotations;

namespace booking.ViewModels;

public class ChooseBusinessCategoriesVm
{
    public int BusinessUserId { get; set; }

    [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất 1 danh mục.")]
    public List<int> CategoryIds { get; set; } = new();

    // để render checkbox
    public List<CategoryItemVm> Categories { get; set; } = new();
}

public class CategoryItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public bool IsChecked { get; set; }
}
