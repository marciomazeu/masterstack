using System.Text.Json.Serialization;

namespace MasterStack.Models.External;

public class JSearchResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public JSearchData Data { get; set; } = new();
}

public class JSearchData
{
    [JsonPropertyName("jobs")]
    public List<JSearchJobItem> Jobs { get; set; } = new();
}

public class JSearchJobItem
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("job_title")]
    public string JobTitle { get; set; } = string.Empty;

    [JsonPropertyName("employer_name")]
    public string EmployerName { get; set; } = string.Empty;

    [JsonPropertyName("job_apply_link")]
    public string? JobApplyLink { get; set; }

    [JsonPropertyName("job_city")]
    public string? JobCity { get; set; }

    [JsonPropertyName("job_state")]
    public string? JobState { get; set; }

    [JsonPropertyName("job_country")]
    public string? JobCountry { get; set; }

    [JsonPropertyName("job_description")]
    public string? JobDescription { get; set; }
}