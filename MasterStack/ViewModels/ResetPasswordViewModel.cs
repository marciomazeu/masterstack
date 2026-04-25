namespace MasterStack.ViewModels
{
    using System.ComponentModel.DataAnnotations;

public class ResetPasswordViewModel
{
    public string Email { get; set; }
    public string Token { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "As senhas não coincidem.")]
    public string ConfirmPassword { get; set; }
}
}

