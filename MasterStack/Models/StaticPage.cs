using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
    public class StaticPage
    {
        public int Id { get; set; }
        
        [Required]
        public string Slug { get; set; } // Ex: "sobre-nos"
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relacionamento: Uma página tem várias traduções
        public virtual ICollection<StaticPageTranslation> Translations { get; set; } = new List<StaticPageTranslation>();
    }

    public class StaticPageTranslation
    {
        public int Id { get; set; }
        public int StaticPageId { get; set; }
        
        [Required]
        public string Culture { get; set; } // pt-BR, en-US, fr-CA
        
        [Required]
        [Display(Name = "Título")]
        public string Title { get; set; }
        
        [Required]
        [Display(Name = "Conteúdo")]
        public string Content { get; set; } // Aqui salvaremos o HTML do Quill

        public virtual StaticPage StaticPage { get; set; }
    }
}