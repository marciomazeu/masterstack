using System.Text.Json;
using MasterStack.Services.JobProviders;
using MasterStack.ViewModels;
using Serilog;

namespace MasterStack.Services.Providers;

public class JSearchJobProvider : IJobProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public string ProviderName => "JSearch";

    public JSearchJobProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<JobDto>> SearchJobsAsync(JobSearchFilter filter)
    {
        var baseQuery = string.IsNullOrWhiteSpace(filter.Query) ? "software developer" : filter.Query;
        var fullQuery = string.IsNullOrWhiteSpace(filter.Location)
            ? baseQuery
            : $"{baseQuery} in {filter.Location}";

        var page = filter.Page < 1 ? 1 : filter.Page;
        // Endpoint padrão estável da JSearch API
        var relativeUrl = $"search?query={Uri.EscapeDataString(fullQuery)}&page={page}&num_pages=1";

        var apiKey = _configuration["RapidAPI:Key"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("[JSearch] A chave 'RapidAPI:Key' não foi encontrada no appsettings.json!");
            return new List<JobDto>();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            request.Headers.TryAddWithoutValidation("x-rapidapi-key", apiKey.Trim());
            request.Headers.TryAddWithoutValidation("x-rapidapi-host", "jsearch.p.rapidapi.com");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("[JSearch] Falha na requisição. Status: {StatusCode} - Motivo: {Reason}",
                    response.StatusCode, response.ReasonPhrase);
                return new List<JobDto>();
            }

            var jsonString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Garante que 'data' existe e é realmente um Array JSON
            if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
            {
                Log.Warning("[JSearch] Resposta inesperada da API (propriedade 'data' não é um array). Payload parcial: {Payload}",
                    jsonString.Length > 200 ? jsonString[..200] : jsonString);
                return new List<JobDto>();
            }

            var mappedJobs = new List<JobDto>();

            foreach (var job in dataElement.EnumerateArray())
            {
                if (job.ValueKind != JsonValueKind.Object) continue;

                var city = GetString(job, "job_city");
                var country = GetString(job, "job_country");

                DateTime? postedDate = null;
                if (job.TryGetProperty("job_posted_at_timestamp", out var tsProp) &&
                    tsProp.ValueKind == JsonValueKind.Number &&
                    tsProp.TryGetInt64(out var unixSeconds))
                {
                    postedDate = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                }

                mappedJobs.Add(new JobDto
                {
                    Id = GetString(job, "job_id"),
                    Title = GetString(job, "job_title"),
                    Company = GetString(job, "employer_name"),
                    Location = !string.IsNullOrWhiteSpace(city) ? city : country,
                    Description = GetString(job, "job_description"),
                    Url = GetString(job, "job_apply_link"),
                    SourceProvider = ProviderName,
                    PostedDate = postedDate
                });
            }

            return mappedJobs;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[JSearch Error] Exceção ao buscar vagas.");
            return new List<JobDto>();
        }
    }

    private static string GetString(JsonElement el, string propName)
    {
        if (el.TryGetProperty(propName, out var prop) && prop.ValueKind != JsonValueKind.Null)
        {
            return prop.ToString();
        }
        return string.Empty;
    }
}