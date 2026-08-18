using System.Text.Json;
using System.Text.Json.Serialization;
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
        var fullQuery = string.IsNullOrWhiteSpace(filter.Location)
            ? filter.Query
            : $"{filter.Query} in {filter.Location}";

        var relativeUrl = $"search-v2?query={Uri.EscapeDataString(fullQuery)}&page={filter.Page}&num_pages=1";

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

            // 1. Desserializa o JSON retornado pela API
            var jsearchResponse = JsonSerializer.Deserialize<JSearchApiResponse>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (jsearchResponse?.Data == null)
            {
                return new List<JobDto>();
            }

            // 2. Mapeia os dados da API para o seu DTO
            var mappedJobs = jsearchResponse.Data.Select(job => new JobDto
            {
                Id = job.JobId ?? string.Empty,
                Title = job.JobTitle ?? string.Empty,
                Company = job.EmployerName ?? string.Empty,
                Location = job.JobCity ?? job.JobCountry ?? string.Empty,
                Description = job.JobDescription ?? string.Empty,
                Url = job.JobApplyLink ?? string.Empty,
                SourceProvider = ProviderName,
                PostedDate = job.JobPostedAtTimestamp.HasValue 
                    ? DateTimeOffset.FromUnixTimeSeconds(job.JobPostedAtTimestamp.Value).DateTime 
                    : null
            }).ToList();

            // 3. Retorna a lista mapeada declarada acima
            return mappedJobs;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[JSearch Error] Exceção ao buscar vagas.");
            return new List<JobDto>();
        }
    }
}

// DTOs auxiliares para deserialização do JSON do JSearch
public class JSearchApiResponse
{
    [JsonPropertyName("data")]
    public List<JSearchJobItem>? Data { get; set; }
}

public class JSearchJobItem
{
    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    [JsonPropertyName("job_title")]
    public string? JobTitle { get; set; }

    [JsonPropertyName("employer_name")]
    public string? EmployerName { get; set; }

    [JsonPropertyName("job_city")]
    public string? JobCity { get; set; }

    [JsonPropertyName("job_country")]
    public string? JobCountry { get; set; }

    [JsonPropertyName("job_description")]
    public string? JobDescription { get; set; }

    [JsonPropertyName("job_apply_link")]
    public string? JobApplyLink { get; set; }

    [JsonPropertyName("job_posted_at_timestamp")]
    public long? JobPostedAtTimestamp { get; set; }
}