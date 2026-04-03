using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterStack.Models
{
    public class BlogPost
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Data de Criação")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now; // Adicione esta linha

        // O Post Pai não tem mais Título, Conteúdo ou ImageUrl.
        // Tudo isso agora vive na lista abaixo (BlogPostTranslations).
        public virtual ICollection<BlogPostTranslation> Translations { get; set; } = new List<BlogPostTranslation>();
    
    [Required]
public string AuthorId { get; set; } = string.Empty; // Mudou de int para string

[ForeignKey("AuthorId")]
public virtual ApplicationUser Author { get; set; } = null!;
    }
}
