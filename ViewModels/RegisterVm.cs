using System.ComponentModel.DataAnnotations;

namespace booking.ViewModels;

public class RegisterVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(6)]
    public string Password { get; set; } = "";

    [Required, Compare("Password", ErrorMessage = "Confirm password does not match.")]
    public string ConfirmPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    public string FullName { get; set; } = "";

    public string AccountType { get; set; } = "user";


}
