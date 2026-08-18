using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterStack.Models
{
   public class Resume
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        
        // Dados neutros (que não mudam com o idioma)
        public string? Phone { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? GitHubUrl { get; set; }
        public string? Website { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Lista de traduções globais do CV (Título, Resumo Profissional)
        public ICollection<ResumeTranslation> Translations { get; set; } = new List<ResumeTranslation>();
        
        // Relacionamentos com itens das seções
        public ICollection<ResumeExperience> Experiences { get; set; } = new List<ResumeExperience>();
        public ICollection<ResumeEducation> Educations { get; set; } = new List<ResumeEducation>();
        public ICollection<ResumeSkill> Skills { get; set; } = new List<ResumeSkill>();
    }

}