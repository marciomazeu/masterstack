using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using MasterStack;
namespace MasterStack.ViewModels
{
public class ProfileViewModel
{
    // Removendo o ResourceType, o .NET para de procurar no arquivo bugado
    [Required(ErrorMessage = "O nome é obrigatório")]
    [Display(Name = "Nome de Exibição")] 
    public string DisplayName { get; set; }

    [Display(Name = "Biografia")]
    public string? Bio { get; set; }

    public string? CurrentImageUrl { get; set; }

    [Display(Name = "Foto de Perfil")]
    public IFormFile? NewImage { get; set; }

    [Url(ErrorMessage = "URL inválida")]
    public string? TwitterUrl { get; set; }

    [Url(ErrorMessage = "URL inválida")]
    public string? LinkedInUrl { get; set; }

    [Url(ErrorMessage = "URL inválida")]
    public string? GitHubUrl { get; set; }
}
}
