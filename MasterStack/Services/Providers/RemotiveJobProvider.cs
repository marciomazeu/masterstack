// Services/Providers/RemotiveJobProvider.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MasterStack.DTOs;
using MasterStack.Services.JobProviders;
using MasterStack.ViewModels;
using Microsoft.Extensions.Logging;

namespace MasterStack.Services.Providers
{
    public class RemotiveJobProvider : IJobProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RemotiveJobProvider> _logger;
        // 🔹 Adicione a propriedade exigida pela interface:
        public string ProviderName => "Remotive";

        public RemotiveJobProvider(HttpClient httpClient, ILogger<RemotiveJobProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("https://remotive.com/");
        }

        public async Task<List<JobDto>> SearchJobsAsync(JobSearchFilter filter)
        {
            try
            {
                // A endpoint pública do Remotive permite filtrar por termo de busca via parâmetro 'search'
                string query = Uri.EscapeDataString(filter.Query ?? "");
                string url = $"api/remote-jobs?search={query}&limit=20";

                var response = await _httpClient.GetFromJsonAsync<RemotiveResponseDto>(url);

                if (response?.Jobs == null || !response.Jobs.Any())
                {
                    return new List<JobDto>();
                }

                // Se houver um filtro de localização, podemos filtrar na memória
                var filteredJobs = response.Jobs.AsEnumerable();
                
                if (!string.IsNullOrWhiteSpace(filter.Location))
                {
                    string loc = filter.Location.ToLower();
                    filteredJobs = filteredJobs.Where(j => 
                        string.IsNullOrEmpty(j.CandidateRequiredLocation) || 
                        j.CandidateRequiredLocation.ToLower().Contains(loc) ||
                        j.CandidateRequiredLocation.ToLower().Contains("worldwide"));
                }

                return filteredJobs.Select(j => new JobDto
                {
                    Title = j.Title,
                    Company = j.CompanyName,
                    Location = string.IsNullOrWhiteSpace(j.CandidateRequiredLocation) ? "Worldwide (Remote)" : j.CandidateRequiredLocation,
                    Url = j.Url,
                    SourceProvider = "Remotive",
                    PostedDate = j.PublicationDate ?? DateTime.UtcNow
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RemotiveJobProvider] Erro ao consultar vagas no Remotive.");
                return new List<JobDto>();
            }
        }
    }
}