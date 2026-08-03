using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
    public class Company
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }

        // Localização e Coordenadas
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? StateOrRegion { get; set; }
        public string? PostalCode { get; set; }
        public string? CountryCode { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Redes Sociais
        public string? LinkedInUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? InstagramUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}