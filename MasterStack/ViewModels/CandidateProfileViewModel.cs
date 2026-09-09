namespace MasterStack.Models.ViewModels
{
    public class CandidateProfileViewModel
    {
    public ApplicationUser Candidate { get; set; } = default!;
        public Resume? Resume { get; set; }
        public string? Summary { get; set; }
        public bool HasTranslation { get; set; } // 👈 Indica se o CV possui dados na cultura ativa
        public List<ResumeExperience> Experiences { get; set; } = new();
        public List<ResumeEducation> Educations { get; set; } = new();
        public List<ResumeSkill> Skills { get; set; } = new();
    }
}