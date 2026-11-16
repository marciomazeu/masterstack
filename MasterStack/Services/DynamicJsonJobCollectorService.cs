using System.Net;
using System.Text.Json;
using Ganss.Xss;
using MasterStack.Data;
using MasterStack.Models;
using Microsoft.EntityFrameworkCore;

namespace MasterStack.Services
{
    public interface IDynamicJsonJobCollectorService
    {
        Task SyncAllActiveSourcesAsync(CancellationToken ct = default);
        Task<(bool Success, int ImportedCount, string Message)> SyncSingleSourceAsync(int sourceId, CancellationToken ct = default);
    }

    public class DynamicJsonJobCollectorService : IDynamicJsonJobCollectorService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DynamicJsonJobCollectorService> _logger;

        public DynamicJsonJobCollectorService(
            HttpClient httpClient,
            IServiceScopeFactory scopeFactory,
            ILogger<DynamicJsonJobCollectorService> logger)
        {
            _httpClient = httpClient;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task SyncAllActiveSourcesAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var activeSourceIds = await context.ExternalJobSources
                .Where(s => s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);

            foreach (var id in activeSourceIds)
            {
                await SyncSingleSourceAsync(id, ct);
            }
        }

        public async Task<(bool Success, int ImportedCount, string Message)> SyncSingleSourceAsync(int sourceId, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var source = await context.ExternalJobSources.FindAsync(new object[] { sourceId }, ct);
            if (source == null)
                return (false, 0, "Fonte não encontrada.");

            try
            {
                string targetUrl = source.ProviderType switch
                {
                    JobSourceProviderType.Greenhouse => $"https://boards-api.greenhouse.io/v1/boards/{source.EndpointOrSlug.Trim()}/jobs?content=true",
                    JobSourceProviderType.Lever => $"https://api.lever.co/v0/postings/{source.EndpointOrSlug.Trim()}?mode=json",
                    JobSourceProviderType.Ashby => $"https://api.ashbyhq.com/posting-api/job-board/{source.EndpointOrSlug.Trim()}",
                    _ => source.EndpointOrSlug.Trim()
                };

                using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                request.Headers.Add("User-Agent", "MasterStackJobs-Aggregator/1.0");

                var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                JsonElement itemsArray = ResolveRootArray(doc.RootElement, source);

                if (itemsArray.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("O JSON retornado não contém um Array válido no caminho especificado.");
                }

                var sanitizer = new HtmlSanitizer();
                int imported = 0;

                foreach (var item in itemsArray.EnumerateArray())
                {
                    var extracted = ExtractJobData(item, source);
                    if (extracted == null) continue;

                    if (!MatchesFilters(extracted.Value.Title, extracted.Value.Location, source))
                        continue;

                    string cleanTitle = Truncate(extracted.Value.Title, 150);
                    string cleanCompany = Truncate(extracted.Value.Company, 150);
                    string normalizedLoc = Truncate(NormalizeQuebecLocation(extracted.Value.Location), 100);

                    bool exists = await context.JobPostings.AnyAsync(j =>
                        j.RedirectUrl == extracted.Value.Url ||
                        (j.Title.ToLower() == cleanTitle.ToLower() &&
                         j.CompanyName.ToLower() == cleanCompany.ToLower()), ct);

                    if (!exists)
                    {
                        var cleanDescription = sanitizer.Sanitize(WebUtility.HtmlDecode(extracted.Value.Description));
                        bool isRemote = normalizedLoc.Contains("remote", StringComparison.OrdinalIgnoreCase) ||
                                        normalizedLoc.Contains("télétravail", StringComparison.OrdinalIgnoreCase) ||
                                        cleanTitle.Contains("remote", StringComparison.OrdinalIgnoreCase);

                        var newJob = new JobPosting
                        {
                            Title = cleanTitle,
                            CompanyName = cleanCompany,
                            Location = normalizedLoc,
                            Description = string.IsNullOrWhiteSpace(cleanDescription) ? $"Vaga importada de {source.Name}" : cleanDescription,
                            RedirectUrl = extracted.Value.Url,
                            SourceProvider = source.Name,
                            IsInternal = false,
                            IsRemote = isRemote,
                            IsActive = true,
                            IsClosed = false,
                            Currency = "CAD",
                            CreatedAt = DateTime.UtcNow,
                            FetchedAt = DateTime.UtcNow
                        };

                        context.JobPostings.Add(newJob);
                        imported++;
                    }
                }

                source.LastSyncedAt = DateTime.UtcNow;
                source.LastImportedCount = imported;
                source.LastErrorMessage = null;
                await context.SaveChangesAsync(ct);

                return (true, imported, $"{imported} novas vagas importadas de {source.Name}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar fonte externa {SourceName} (ID: {SourceId})", source.Name, source.Id);
                source.LastSyncedAt = DateTime.UtcNow;
                source.LastErrorMessage = Truncate(ex.Message, 450);
                await context.SaveChangesAsync(ct);

                return (false, 0, $"Erro: {ex.Message}");
            }
        }

        private JsonElement ResolveRootArray(JsonElement root, ExternalJobSource source)
        {
            if (source.ProviderType == JobSourceProviderType.Greenhouse || source.ProviderType == JobSourceProviderType.Ashby)
                return root.TryGetProperty("jobs", out var ghJobs) ? ghJobs : default;

            if (source.ProviderType == JobSourceProviderType.Lever)
                return root;

            if (string.IsNullOrWhiteSpace(source.JsonRootArrayPath))
                return root;

            var current = root;
            foreach (var segment in source.JsonRootArrayPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
                    current = next;
                else
                    return default;
            }
            return current;
        }

        private (string Title, string Company, string Location, string Description, string Url)? ExtractJobData(JsonElement item, ExternalJobSource source)
        {
            if (source.ProviderType == JobSourceProviderType.Greenhouse)
            {
                string title = GetString(item, "title");
                string url = GetString(item, "absolute_url");
                string desc = GetString(item, "content");
                string loc = item.TryGetProperty("location", out var locObj) ? GetString(locObj, "name") : "Québec, Canada";

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) return null;
                return (title, source.Name, loc, desc, url);
            }

            if (source.ProviderType == JobSourceProviderType.Lever)
            {
                string title = GetString(item, "text");
                string url = GetString(item, "hostedUrl");
                string desc = GetString(item, "descriptionPlain");
                string loc = item.TryGetProperty("categories", out var catObj) ? GetString(catObj, "location") : "Québec, Canada";

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) return null;
                return (title, source.Name, loc, desc, url);
            }

            if (source.ProviderType == JobSourceProviderType.Ashby)
            {
                string title = GetString(item, "title");
                string url = GetString(item, "jobUrl");
                string desc = GetString(item, "descriptionHtml");
                string loc = GetString(item, "location");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) return null;
                return (title, source.Name, loc, desc, url);
            }

            // CustomJson dinâmico
            string cTitle = GetString(item, source.MapTitleField ?? "title");
            string cUrl = GetString(item, source.MapUrlField ?? "url");
            string cDesc = GetString(item, source.MapDescriptionField ?? "description");
            string cLoc = GetString(item, source.MapLocationField ?? "location");
            string cComp = !string.IsNullOrWhiteSpace(source.MapCompanyField)
                ? GetString(item, source.MapCompanyField)
                : source.Name;

            if (string.IsNullOrWhiteSpace(cTitle) || string.IsNullOrWhiteSpace(cUrl)) return null;
            return (cTitle, string.IsNullOrWhiteSpace(cComp) ? source.Name : cComp, cLoc, cDesc, cUrl);
        }

        private string GetString(JsonElement el, string propName)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(propName, out var prop))
                return prop.ToString();
            return string.Empty;
        }

        private bool MatchesFilters(string title, string location, ExternalJobSource source)
        {
            if (!string.IsNullOrWhiteSpace(source.LocationFilter))
            {
                var allowedLocs = source.LocationFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!allowedLocs.Any(l => location.Contains(l, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(source.TechKeywordsFilter))
            {
                var keywords = source.TechKeywordsFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!keywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }

        private string NormalizeQuebecLocation(string rawLocation)
        {
            if (string.IsNullOrWhiteSpace(rawLocation)) return "Québec, Canada";
            if (rawLocation.Contains("Montreal", StringComparison.OrdinalIgnoreCase) || rawLocation.Contains("Montréal", StringComparison.OrdinalIgnoreCase))
                return "Montréal, QC";
            if (rawLocation.Contains("Quebec City", StringComparison.OrdinalIgnoreCase) || rawLocation.Contains("Ville de Québec", StringComparison.OrdinalIgnoreCase))
                return "Québec, QC";
            return rawLocation;
        }

        private string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}