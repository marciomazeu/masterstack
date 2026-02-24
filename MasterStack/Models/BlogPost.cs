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
    
    // Vínculo com o Autor
    public int AuthorProfileId { get; set; } // FK
    public virtual AuthorProfile Author { get; set; } // Propriedade de Navegação
    }
}
