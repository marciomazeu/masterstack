using System.Collections.Generic;
using MasterStack.Models;

namespace MasterStack.ViewModels
{
    public class EnterprisesPageViewModel
    {
        public List<CompanyDistanceViewModel> Companies { get; set; } = new();
        public List<JobPosting> JobPostings { get; set; } = new();
        public ApplicationUser? User { get; set; }

        // 💼 Atributos auxiliares para facilitar o acesso na View
        public string PreferredJobTitle => User?.PreferredJobTitle ?? "developer";
        public int SearchRadiusKm => User?.SearchRadiusKm > 0 ? User.SearchRadiusKm : 50;
    }
}