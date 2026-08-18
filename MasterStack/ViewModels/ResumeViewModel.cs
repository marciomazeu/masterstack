using System.ComponentModel.DataAnnotations;

namespace MasterStack.ViewModels
{
    public class ResumeViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Nome Completo")]
        public string? FullName { get; set; }

        [Display(Name = "Título Profissional")]
        public string? ProfessionalTitle { get; set; }

        [Display(Name = "E-mail de Contato")]
        [EmailAddress]
        public string? Email { get; set; }

        [Display(Name = "Telefone / WhatsApp")]
        public string? Phone { get; set; }

        [Display(Name = "Cidade")]
        public string? City { get; set; }

        [Display(Name = "Estado / Província")]
        public string? StateOrProvince { get; set; }

        [Display(Name = "LinkedIn (URL)")]
        public string? LinkedInUrl { get; set; }

        [Display(Name = "GitHub (URL)")]
        public string? GitHubUrl { get; set; }

        [Display(Name = "Resumo Profissional")]
        [MaxLength(2000)]
        public string? Summary { get; set; }

        [Display(Name = "Habilidades (separadas por vírgula)")]
        public string? SkillsCsv { get; set; }

        public List<ResumeExperienceViewModel> Experiences { get; set; } = new();
        public List<ResumeEducationViewModel> Educations { get; set; } = new();
    }

}