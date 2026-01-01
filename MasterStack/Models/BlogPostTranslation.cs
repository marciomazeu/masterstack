using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterStack.Models
{
    public class BlogPostTranslation
    {
        [Key]
        public int Id { get; set; }

        // Chave Estrangeira para o BlogPost pai
        [Required]
        public int BlogPostId { get; set; }

        [ForeignKey("BlogPostId")]
        public virtual BlogPost BlogPost { get; set; }

        [Required]
        [StringLength(10)] // Ex: "pt-BR", "en-US"
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
    }
}
