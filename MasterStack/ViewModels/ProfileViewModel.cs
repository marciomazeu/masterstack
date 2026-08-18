using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MasterStack.ViewModels
{
    public class ProfileViewModel
    {
        [Display(Name = "Nome Exibido")]
        public string? DisplayName { get; set; }

        public string? Email { get; set; }

        [Display(Name = "Biografia")]
        public string? Bio { get; set; }

        // 🌐 Adicionados para suporte multi-idioma na View de edição do Perfil
        [Display(Name = "Biografia (Inglês)")]
        public string? Bio_EN { get; set; }

        [Display(Name = "Biografia (Francês)")]
        public string? Bio_FR { get; set; }

        // 🌐 Redes Sociais
        [Url(ErrorMessage = "Insira uma URL válida para o Facebook")]
        public string? FacebookUrl { get; set; }

        [Url(ErrorMessage = "Insira uma URL válida para o Instagram")]
        public string? InstagramUrl { get; set; }

        [Url(ErrorMessage = "Insira uma URL válida para o X/Twitter")]
        public string? TwitterUrl { get; set; }

        [Url(ErrorMessage = "Insira uma URL válida para o LinkedIn")]
        public string? LinkedInUrl { get; set; }

        [Url(ErrorMessage = "Insira uma URL válida para o GitHub")]
        public string? GitHubUrl { get; set; }

        public string? CurrentImageUrl { get; set; }

        [Display(Name = "Nova Imagem de Perfil")]
        public IFormFile? NewImage { get; set; }

        public bool IsAuthorOrAdmin { get; set; }
        public bool IsTwoFactorEnabled { get; set; }

        // 🏠 Localização
        [Display(Name = "StreetAddress")]
        [Required(ErrorMessage = "RequiredField")]
        public string StreetAddress { get; set; } = string.Empty;
        [Display(Name = "City")]
        [Required(ErrorMessage = "RequiredField")]
        public string City { get; set; } = string.Empty;
        public string? StateOrRegion { get; set; }
        [Display(Name = "PostalCode")]
        [Required(ErrorMessage = "RequiredField")]
        public string PostalCode { get; set; } = string.Empty;
        [Display(Name = "Country")] // Chave que será traduzida no arquivo .resx
        [Required(ErrorMessage = "RequiredField")]
        public string CountryCode { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        [Display(Name = "Cargo Preferido")]
        public string? PreferredJobTitle { get; set; }

        [Display(Name = "Raio de Busca (KM)")]
        [Range(1, 500, ErrorMessage = "Informe um raio válido entre 1 e 500 KM.")]
        public int SearchRadiusKm { get; set; } = 50;
    }
}