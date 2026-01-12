using System.ComponentModel.DataAnnotations;

namespace booking.ViewModels;

public class LoginVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}
