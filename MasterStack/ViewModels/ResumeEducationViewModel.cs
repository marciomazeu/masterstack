using System.ComponentModel.DataAnnotations;

namespace MasterStack.ViewModels
{
       public class ResumeEducationViewModel
    {
        public string Culture { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;
        public string Degree { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}