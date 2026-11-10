using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.Models
{
    [Index(nameof(AuthorId))]
    public class BlogPost
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Data de Criação")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Data de Atualização")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // 🔗 Automação do LinkedIn
        public string? LinkedInPostText { get; set; }
        public bool PublishToLinkedIn { get; set; } = false;
        public bool IsLinkedInPublished { get; set; } = false;
        public string? LinkedInPostUrl { get; set; }

        // 🌐 Traduções do Post
        public virtual ICollection<BlogPostTranslation> Translations { get; set; } = new List<BlogPostTranslation>();
    
        [Required]
        public string AuthorId { get; set; } = string.Empty;

        [ForeignKey("AuthorId")]
        public virtual ApplicationUser Author { get; set; } = null!;
    }
}