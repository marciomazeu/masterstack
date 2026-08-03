using System;
using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
    public class AffiliateProduct
    {
        [Key]
        public int Id { get; set; }

        // Identificador único/Slug interno (ex: "book-clean-code" ou "curso-aspnet")
        [Required]
        [StringLength(100)]
        public string ProductCode { get; set; } = string.Empty;

        // Plataforma de Origem (Amazon, Udemy, Hotmart, etc.)
        [Required]
        [StringLength(50)]
        public string Network { get; set; } = "Amazon"; 

        // Categoria para facilitar o vínculo automático com posts
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        // Preço sugerido ou exibido (opcional)
        public decimal? Price { get; set; }
        public string Currency { get; set; } = "CAD";

        // --- LINKS LOCALIZADOS (Geotargeting / Idiomas) ---
        [Required]
        public string TargetUrl_EN { get; set; } = string.Empty;
        public string TargetUrl_PT { get; set; } = string.Empty;
        public string TargetUrl_FR { get; set; } = string.Empty;

        // --- CONTEÚDO LOCALIZADO ---
        [Required]
        [StringLength(150)]
        public string Title_EN { get; set; } = string.Empty;
        public string Title_PT { get; set; } = string.Empty;
        public string Title_FR { get; set; } = string.Empty;

        public string Description_EN { get; set; } = string.Empty;
        public string Description_PT { get; set; } = string.Empty;
        public string Description_FR { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        // --- AUTOMAÇÃO E EXPIRAÇÃO ---
        public DateTime? ExpirationDate { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Link alternativo padrão de fallback caso o principal expire
        public string? FallbackUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}