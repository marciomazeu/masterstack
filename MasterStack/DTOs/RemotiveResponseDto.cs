// DTOs/RemotiveResponseDto.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MasterStack.DTOs
{
    public class RemotiveResponseDto
    {
        [JsonPropertyName("jobs")]
        public List<RemotiveJobDto> Jobs { get; set; } = new();
    }

    public class RemotiveJobDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("company_name")]
        public string CompanyName { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("candidate_required_location")]
        public string CandidateRequiredLocation { get; set; } = string.Empty;

        [JsonPropertyName("publication_date")]
        public DateTime? PublicationDate { get; set; }
    }
}