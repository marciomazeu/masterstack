using System.Globalization;
using System.Net;
using System.Text;
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
                string rawEndpoint = source.EndpointOrSlug.Trim();
                string targetUrl;

                if (rawEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    rawEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    targetUrl = rawEndpoint;
                }
                else
                {
                    targetUrl = source.ProviderType switch
                    {
                        JobSourceProviderType.Greenhouse => $"https://boards-api.greenhouse.io/v1/boards/{rawEndpoint}/jobs?content=true",
                        JobSourceProviderType.Lever => $"https://api.lever.co/v0/postings/{rawEndpoint}?mode=json",
                        JobSourceProviderType.Ashby => $"https://api.ashbyhq.com/posting-api/job-board/{rawEndpoint}",
                        _ => rawEndpoint
                    };
                }

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

        private static string ExtractJsonString(JsonElement element, string? fieldPath)
        {
            if (string.IsNullOrWhiteSpace(fieldPath)) return string.Empty;

            JsonElement current = element;
            foreach (var part in fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out var next))
                    return string.Empty;
                current = next;
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => current.ToString(),
                _ => string.Empty
            };
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
                string title = ExtractJsonString(item, "title");
                string url = ExtractJsonString(item, "absolute_url");
                string desc = ExtractJsonString(item, "content");
                string loc = ExtractJsonString(item, "location.name");
                if (string.IsNullOrWhiteSpace(loc)) loc = "Québec, Canada";

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) return null;
                return (title, source.Name, loc, desc, url);
            }

            if (source.ProviderType == JobSourceProviderType.Lever)
            {
                string title = ExtractJsonString(item, "text");
                string url = ExtractJsonString(item, "hostedUrl");
                string desc = ExtractJsonString(item, "descriptionPlain");
                string loc = ExtractJsonString(item, "categories.location");
                if (string.IsNullOrWhiteSpace(loc)) loc = "Québec, Canada";

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) return null;
                return (title, source.Name, loc, desc, url);
            }

            if (source.ProviderType == JobSourceProviderType.Ashby)
            {
                string title = ExtractJsonString(item, "title");
                string url = ExtractJsonString(item, "jobUrl");
                string desc = ExtractJsonString(item, "descriptionHtml");
                string loc = ExtractJsonString(item, "location");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) return null;
                return (title, source.Name, loc, desc, url);
            }

            // CustomJson dinâmico com suporte a caminhos aninhados (ex: "company.name", "location.city")
            string cTitle = ExtractJsonString(item, source.MapTitleField ?? "title");
            string cUrl = ExtractJsonString(item, source.MapUrlField ?? "url");
            string cDesc = ExtractJsonString(item, source.MapDescriptionField ?? "description");
            string cLoc = ExtractJsonString(item, source.MapLocationField ?? "location");
            string cComp = !string.IsNullOrWhiteSpace(source.MapCompanyField)
                ? ExtractJsonString(item, source.MapCompanyField)
                : source.Name;

            if (string.IsNullOrWhiteSpace(cTitle) || string.IsNullOrWhiteSpace(cUrl)) return null;
            return (cTitle, string.IsNullOrWhiteSpace(cComp) ? source.Name : cComp, cLoc, cDesc, cUrl);
        }

        private bool MatchesFilters(string title, string location, ExternalJobSource source)
        {
            if (!string.IsNullOrWhiteSpace(source.LocationFilter))
            {
                string normalizedLocation = RemoveDiacritics(location);
                var allowedLocs = source.LocationFilter
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(RemoveDiacritics);

                if (!allowedLocs.Any(l => normalizedLocation.Contains(l, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(source.TechKeywordsFilter))
            {
                string normalizedTitle = RemoveDiacritics(title);
                var keywords = source.TechKeywordsFilter
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(RemoveDiacritics);

                if (!keywords.Any(k => normalizedTitle.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private string NormalizeQuebecLocation(string rawLocation)
        {
            if (string.IsNullOrWhiteSpace(rawLocation)) return "Québec, Canada";
            string clean = RemoveDiacritics(rawLocation);

            if (clean.Contains("Montreal", StringComparison.OrdinalIgnoreCase))
                return "Montréal, QC";
            if (clean.Contains("Quebec City", StringComparison.OrdinalIgnoreCase) || clean.Contains("Ville de Quebec", StringComparison.OrdinalIgnoreCase))
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