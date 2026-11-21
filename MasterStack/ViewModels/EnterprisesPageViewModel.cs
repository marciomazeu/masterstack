using System.Collections.Generic;
using MasterStack.Models;

namespace MasterStack.ViewModels
{
    public class EnterprisesPageViewModel
    {
        public List<CompanyDistanceViewModel> Companies { get; set; } = new();
        
        // 💼 Vagas cadastradas diretamente na plataforma (SQL Server / MasterStack)
        public List<JobPosting> LocalJobs { get; set; } = new();

        // 🌐 Vagas agregadas via APIs parceiras (Adzuna, Jooble, JSearch, Remotive, etc.)
        public List<JobPosting> JobPosting { get; set; } = new();

        // 👤 Dados do Usuário Logado
        public ApplicationUser? User { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalLocalJobsCount { get; set; }
        public int TotalExternalJobsCount { get; set; }
        public string ActiveTab { get; set; } = "local";

        public int TotalLocalPages => (int)Math.Ceiling((double)TotalLocalJobsCount / PageSize);
        public int TotalExternalPages => (int)Math.Ceiling((double)TotalExternalJobsCount / PageSize);

        // 🛠️ Propriedades auxiliares facilitadoras para a View
        public string PreferredJobTitle => User?.PreferredJobTitle ?? "developer";
        public int SearchRadiusKm => User?.SearchRadiusKm > 0 ? User.SearchRadiusKm : 50;
    }
}