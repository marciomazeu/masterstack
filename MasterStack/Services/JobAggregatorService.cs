using MasterStack.Data;
using MasterStack.Services.JobProviders;
using MasterStack.ViewModels;
using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MasterStack.Services
{
    public class JobAggregatorService
    {
        private readonly IEnumerable<IJobProvider> _providers;
        private readonly ILogger<JobAggregatorService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResource> _localizer;

        private const int CacheDurationHours = 6;

        public JobAggregatorService(
            IEnumerable<IJobProvider> providers,
            ILogger<JobAggregatorService> logger, 
            ApplicationDbContext context)
        {
            _providers = providers;
            _logger = logger;
            _context = context;
        }

        public async Task<List<JobDto>> AggregateJobsAsync(JobSearchFilter filter)
        {
            string normalizedQuery = (filter.Query ?? "").Trim().ToLower();
            string normalizedCity = (filter.Location ?? "").Trim().ToLower();

            DateTime cacheThreshold = DateTime.UtcNow.AddHours(-CacheDurationHours);

            // ============================================================
            // 1. CHECAGEM DE CACHE NO POSTGRESQL
            // ============================================================
            var cachedJobs = await _context.JobPostings
                .Where(j => j.SearchQuery == normalizedQuery &&
                            j.SearchCity == normalizedCity &&
                            j.FetchedAt >= cacheThreshold)
                .ToListAsync();

            if (cachedJobs.Any())
            {
                _logger.LogInformation("[JobCache] Hit! Retornando {Count} vagas do banco de dados local para '{Query}' em '{Location}'.",
                    cachedJobs.Count, normalizedQuery, normalizedCity);

                // Converte do banco para DTOs de retorno
                return cachedJobs.Select(j => new JobDto
                {
                    Title = j.Title,
                    Company = j.CompanyName,
                    Location = j.Location,
                    Url = j.RedirectUrl,
                    SourceProvider = j.SourceProvider,
                    PostedDate = j.CreatedAt
                }).ToList();
            }

            _logger.LogInformation("[JobCache] Miss! Executando chamadas externas para '{Query}' em '{Location}'.", 
                normalizedQuery, normalizedCity);

            // ============================================================
            // 2. CHAMADA EM PARALELO ÀS APIS EXTERNAS
            // ============================================================
            var tasks = _providers.Select(async provider =>
            {
                try
                {
                    return await provider.SearchJobsAsync(filter);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[JobCache] Erro ao buscar vagas no provedor {Provider}", provider.GetType().Name);
                    return new List<JobDto>();
                }
            });

            var results = await Task.WhenAll(tasks);
            var freshJobs = results.SelectMany(r => r).ToList();

            if (!freshJobs.Any())
            {
                return new List<JobDto>();
            }

            // Remove duplicados da resposta combinada
            var distinctFreshJobs = freshJobs
                .GroupBy(j => string.IsNullOrEmpty(j.Url) ? $"{j.Title}-{j.Company}" : j.Url)
                .Select(g => g.First())
                .ToList();

            // ============================================================
            // 3. PERSISTÊNCIA E ATUALIZAÇÃO NO BANCO DE DADOS
            // ============================================================
            // Remove buscas antigas/expiradas do mesmo termo/cidade para não inflar a tabela
            var obsoleteJobs = _context.JobPostings
                .Where(j => j.SearchQuery == normalizedQuery && j.SearchCity == normalizedCity);
            
            _context.JobPostings.RemoveRange(obsoleteJobs);

            // Mapeia os novos DTOs para Entidades do Entity Framework
            var entitiesToSave = distinctFreshJobs.Select(dto => new JobPosting
            {
                Title = dto.Title,
                CompanyName = dto.Company,
                Location = dto.Location,
                RedirectUrl = dto.Url,
                SourceProvider = string.IsNullOrWhiteSpace(dto.SourceProvider) ? "Aggregator" : dto.SourceProvider,
                SearchQuery = normalizedQuery,
                SearchCity = normalizedCity,
                FetchedAt = DateTime.UtcNow,
                CreatedAt = dto.PostedDate ?? DateTime.UtcNow
            }).ToList();

            await _context.JobPostings.AddRangeAsync(entitiesToSave);
            await _context.SaveChangesAsync();

            return distinctFreshJobs;
        }
    }
}