using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterStack.Models
{
    public class BlogPost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Data de Criação")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "URL da Imagem")]
        public string? ImageUrl { get; set; }

        // Propriedade de Navegação: Um Post tem muitas Traduções
        public virtual ICollection<BlogPostTranslation> Translations { get; set; } = new List<BlogPostTranslation>();
    }
}
