using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
    public class BlogPost
    {
        [Key]
        public int Id { get; set; }

        // Chave para identificar qual idioma este post pertence (pt-BR, en-US, etc)
        [Required]
        public string Culture { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
