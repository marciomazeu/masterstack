using MasterStack.Models;

namespace MasterStack.ViewModels
{
    public class EnterprisesPageViewModel
    {
        public List<CompanyDistanceViewModel> Companies { get; set; } = new();
        public List<JobPosting> JobPostings { get; set; } = new();
        public ApplicationUser? User { get; set; }
    }
}