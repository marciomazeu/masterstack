using Microsoft.AspNetCore.Identity;

namespace MasterStack.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? Bio { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? GitHubUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? InstagramUrl { get; set; }

        // 🏠 Localização
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? StateOrRegion { get; set; }
        public string? PostalCode { get; set; }
        public string? CountryCode { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        // 🟢 NOVOS CAMPOS DE PREFERÊNCIA DO USUÁRIO
        public int SearchRadiusKm { get; set; } = 25; // Padrão: 25 km
        public string PreferredJobTitle { get; set; } = "developer"; // Padrão: "developer"

        // 📝 Propriedade de navegação para os Posts (Resolve o erro no ApplicationDbContext)
        public virtual ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();

        // 🌐 Tradução dinâmica da biografia
        public virtual ICollection<UserTranslation> Translations { get; set; } = new List<UserTranslation>();
    }
}