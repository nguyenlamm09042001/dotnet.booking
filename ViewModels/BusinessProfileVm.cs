using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace booking.ViewModels;

public class BusinessProfileVm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên doanh nghiệp")]
    public string? FullName { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string? Email { get; set; }

    // để hiển thị preview ảnh hiện tại
    public string? Avatar { get; set; }

    // file upload
    public IFormFile? AvatarFile { get; set; }

    // chỉ để hiển thị
    public string? Role { get; set; }
}
