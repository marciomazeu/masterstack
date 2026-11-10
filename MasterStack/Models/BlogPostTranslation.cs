using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.Models
{
    [Index(nameof(Culture), nameof(Slug), IsUnique = true)]
    [Index(nameof(BlogPostId))]
    public class BlogPostTranslation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BlogPostId { get; set; }

        [ForeignKey("BlogPostId")]
        public virtual BlogPost? BlogPost { get; set; }

        [Required]
        [StringLength(15)]
        public string Culture { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "Título")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Conteúdo")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [StringLength(250)]
        [Display(Name = "SEO Slug")]
        public string Slug { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        [StringLength(200)]
        [Display(Name = "Texto Alternativo da Imagem (SEO)")]
        public string? ImageAltText { get; set; }

        [NotMapped]
        public IFormFile? ImageFile { get; set; }

        [ForeignKey("Culture")]
        public virtual Language? Language { get; set; }

        [StringLength(100)]
        [Display(Name = "Título SEO")]
        public string? MetaTitle { get; set; }

        [StringLength(160)]
        [Display(Name = "Descrição SEO")]
        public string? MetaDescription { get; set; }

        [Display(Name = "Palavras-chave SEO")]
        public string? MetaKeywords { get; set; }

        [Display(Name = "Publicado")]
        public bool IsPublished { get; set; } = false;

        public bool IsDeleted { get; set; } = false;
    }
}