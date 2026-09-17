using System.ComponentModel.DataAnnotations;
using MasterStack.Attributes;

namespace MasterStack.ViewModels
{
    public class EditTranslationViewModel
    {
        public int TranslationId { get; set; }
        public string Culture { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }
        // --- NOVOS CAMPOS DE SEO ---
        [Required(ErrorMessage = "O Slug é obrigatório para o SEO")]
        public string Slug { get; set; }

        [StringLength(160, ErrorMessage = "A descrição deve ter no máximo 160 caracteres")]
        public string? MetaDescription { get; set; }

        public string? MetaKeywords { get; set; }
        // ---------------------------

        public string? CurrentImageUrl { get; set; }
        
        [MaxFileSize(5 * 1024 * 1024)]
        [AllowedExtensions(new string[] { ".jpg", ".jpeg", ".png", ".webp" })]
        public IFormFile? ImageFile { get; set; }
        public bool IsPublished { get; set; } // Novo campo para status de publicação
    }
}
