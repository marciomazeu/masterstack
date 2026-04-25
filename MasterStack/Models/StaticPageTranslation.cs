  using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
  public class StaticPageTranslation
    {
        public int Id { get; set; }
        public int StaticPageId { get; set; }
        
        [Required]
        public string Culture { get; set; } 
        
        [Required]
        public string Title { get; set; }
        
        [Required]
        public string Content { get; set; }

        // Novos campos de SEO que combinamos
        [MaxLength(70)]
        public string? SeoTitle { get; set; }

        [MaxLength(160)]
        public string? SeoDescription { get; set; }

        // Navegação de volta para o Pai
        public virtual StaticPage StaticPage { get; set; }
    }
}