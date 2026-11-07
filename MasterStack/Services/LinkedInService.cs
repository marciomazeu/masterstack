using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class LinkedInService : ILinkedInService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LinkedInService> _logger;

    public LinkedInService(HttpClient httpClient, IConfiguration configuration, ILogger<LinkedInService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SharePostAsync(string textContent, string articleUrl)
    {
        var accessToken = _configuration["LinkedIn:AccessToken"];
        var authorUrn = _configuration["LinkedIn:OrganizationUrn"]; // ex: "urn:li:organization:12345678" ou "urn:li:person:abcdef"

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(authorUrn))
        {
            _logger.LogWarning("Token do LinkedIn ou URN da organização não configurados.");
            return false;
        }

        var requestUri = "https://api.linkedin.com/v2/ugcPosts";
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _httpClient.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");

        var payload = new
        {
            author = authorUrn,
            lifecycleState = "PUBLISHED",
            specificContent = new
            {
                dictionary_com_linkedin_ugc_ShareContent = new
                {
                    shareCommentary = new
                    {
                        text = $"{textContent}\n\n👉 {articleUrl}"
                    },
                    shareMediaCategory = "ARTICLE",
                    media = new[]
                    {
                        new
                        {
                            status = "READY",
                            originalUrl = articleUrl
                        }
                    }
                }
            },
            visibility = new
            {
                dictionary_com_linkedin_ugc_Visibility = new
                {
                    com_linkedin_ugc_MemberNetworkVisibility = "PUBLIC"
                }
            }
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(requestUri, jsonContent);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Post publicado com sucesso no LinkedIn!");
                return true;
            }

            var errorResponse = await response.Content.ReadAsStringAsync();
            _logger.LogError("Erro ao publicar no LinkedIn. Status: {Status}, Resposta: {Error}", response.StatusCode, errorResponse);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao chamar a API do LinkedIn.");
            return false;
        }
    }
}