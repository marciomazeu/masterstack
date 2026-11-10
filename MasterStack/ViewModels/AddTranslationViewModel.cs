using System.ComponentModel.DataAnnotations;
using MasterStack.Attributes;
using Microsoft.AspNetCore.Http;

namespace MasterStack.ViewModels
{
    public class AddTranslationViewModel
    {
        [Required]
        public int BlogPostId { get; set; }

        [Required(ErrorMessage = "É obrigatório selecionar um idioma.")]
        [Display(Name = "Idioma")]
        public string SelectedCulture { get; set; } = string.Empty;

        [Required(ErrorMessage = "O título é obrigatório.")]
        [StringLength(200, ErrorMessage = "O título não pode ter mais de 200 caracteres.")]
        [Display(Name = "Título")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "O conteúdo do artigo é obrigatório.")]
        [Display(Name = "Conteúdo")]
        public string Content { get; set; } = string.Empty;

        [MaxFileSize(5 * 1024 * 1024)] // 5MB limit
        [AllowedExtensions(new string[] { ".jpg", ".jpeg", ".png", ".webp" })]
        [Display(Name = "Imagem de Capa")]
        public IFormFile? ImageFile { get; set; }

        public bool IsPublished { get; set; } = false;

        [StringLength(250)]
        [Display(Name = "Slug Amigável")]
        public string? Slug { get; set; }

        [StringLength(100)]
        [Display(Name = "Título SEO")]
        public string? MetaTitle { get; set; }

        [StringLength(160)]
        [Display(Name = "Meta Description SEO")]
        public string? MetaDescription { get; set; }

        [Display(Name = "Palavras-Chave SEO")]
        public string? MetaKeywords { get; set; }

        // 🔗 Campos para integração opcional com o LinkedIn ao adicionar tradução
        [Display(Name = "Texto para o LinkedIn")]
        public string? LinkedInPostText { get; set; }

        [Display(Name = "Publicar no LinkedIn agora")]
        public bool PublishToLinkedIn { get; set; } = false;
    }
}