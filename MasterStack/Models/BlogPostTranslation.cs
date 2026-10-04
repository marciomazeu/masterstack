using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace MasterStack.Models
{
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
        public string Culture { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Título")]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Conteúdo")]
        public string Content { get; set; }

        [Required]
        [StringLength(250)]
        [Display(Name = "SEO Slug")]
        public string Slug { get; set; }

        public string? ImageUrl { get; set; }

        [NotMapped]
        public IFormFile? ImageFile { get; set; }

        public virtual Language? Language { get; set; }

        // 💡 [NotMapped] adicionado para impedir o erro de coluna inexistente no SQL
        [NotMapped]
        [StringLength(100)]
        [Display(Name = "Título SEO")]
        public string? MetaTitle { get; set; }

        [StringLength(160)]
        public string? MetaDescription { get; set; }

        public string? MetaKeywords { get; set; }

        [Display(Name = "Publicado")]
        public bool IsPublished { get; set; } = false;

        public bool IsDeleted { get; set; } = false;
    }
}