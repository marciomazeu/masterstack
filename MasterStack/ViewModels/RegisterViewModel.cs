using System.ComponentModel.DataAnnotations;
using MasterStack; // Ajuste para o seu namespace onde está o SharedResource

public class RegisterViewModel
{
    [Required(ErrorMessageResourceName = "Err_Required", ErrorMessageResourceType = typeof(SharedResource))]
    [EmailAddress(ErrorMessageResourceName = "Err_Email", ErrorMessageResourceType = typeof(SharedResource))]
    [Display(Name = "EmailLabel", ResourceType = typeof(SharedResource))]
    public string Email { get; set; }

    [Required(ErrorMessageResourceName = "Err_Required", ErrorMessageResourceType = typeof(SharedResource))]
    [StringLength(100, MinimumLength = 6, ErrorMessageResourceName = "Err_PasswordLength", ErrorMessageResourceType = typeof(SharedResource))]
    [DataType(DataType.Password)]
    [Display(Name = "PasswordLabel", ResourceType = typeof(SharedResource))]
    public string Password { get; set; }

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessageResourceName = "Err_PasswordMatch", ErrorMessageResourceType = typeof(SharedResource))]
    [Display(Name = "ConfirmPasswordLabel", ResourceType = typeof(SharedResource))]
    public string ConfirmPassword { get; set; }
}