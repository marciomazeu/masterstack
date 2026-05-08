using System.ComponentModel.DataAnnotations;
using MasterStack; // Para acessar SharedResource

namespace MasterStack.ViewModels;
public class EditUserViewModel
{
    public required string Id { get; set; }

    [Display(Name = "DisplayName", ResourceType = typeof(SharedResource))]
    [Required(ErrorMessageResourceName = "RequiredField", ErrorMessageResourceType = typeof(SharedResource))]
    [StringLength(100, ErrorMessageResourceName = "MaxLengthError", ErrorMessageResourceType = typeof(SharedResource))]
    public required string DisplayName { get; set; }

    [Display(Name = "Email", ResourceType = typeof(SharedResource))]
    [Required(ErrorMessageResourceName = "RequiredField", ErrorMessageResourceType = typeof(SharedResource))]
    [EmailAddress(ErrorMessageResourceName = "InvalidEmail", ErrorMessageResourceType = typeof(SharedResource))]
    public required string Email { get; set; }
    
    // Lista de cargos que o usuário possui atualmente
    public IList<string> UserRoles { get; set; } = new List<string>();
    
    // Lista de todos os cargos disponíveis no sistema para o Checkbox
    public IEnumerable<string> AllRoles { get; set; } = new List<string>();
}