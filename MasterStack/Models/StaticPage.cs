using System.ComponentModel.DataAnnotations;

namespace MasterStack.Models
{
    public class StaticPage
    {
        public int Id { get; set; }
        
        [Required]
        public string Slug { get; set; } // Ex: "sobre-nos"
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // --- CAMPO PARA SOFT DELETE ---
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        // ------------------------------

        // Relacionamento: Uma página tem várias traduções
        public virtual ICollection<StaticPageTranslation> Translations { get; set; } = new List<StaticPageTranslation>();
    }

  
}