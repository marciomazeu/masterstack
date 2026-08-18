using System.ComponentModel.DataAnnotations;

namespace MasterStack.ViewModels
{
    public class ResumeExperienceViewModel
    {
        public string Company { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Description { get; set; }
    }
}