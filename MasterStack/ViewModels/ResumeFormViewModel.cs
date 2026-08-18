namespace MasterStack.ViewModels
{
    public class ResumeFormViewModel
    {
        public int ResumeId { get; set; }
        public string Culture { get; set; } = "pt-BR"; // Idioma que está sendo editado/salvo

        // Dados Globais Traduzidos
        public string JobTitle { get; set; }
        public string Summary { get; set; }

        // Dados Pessoais / Contato (Gerais)
        public string Phone { get; set; }
        public string LinkedInUrl { get; set; }
        public string GithubUrl { get; set; }
        public string? SkillsCsv { get; set; }
        // Listas
        public List<ResumeExperienceViewModel> Experiences { get; set; } = new();
        public List<ResumeEducationViewModel> Educations { get; set; } = new();
        public List<string> Skills { get; set; } = new();
    }
}