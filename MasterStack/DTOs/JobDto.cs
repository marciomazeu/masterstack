namespace MasterStack.ViewModels
{
    public class JobDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string SourceProvider { get; set; } = string.Empty; // ex: "JSearch", "Adzuna"
        public DateTime? PostedDate { get; set; } = DateTime.UtcNow;
        public bool IsRemote { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class JobSearchFilter
    {
        public string Query { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Culture { get; set; } = "pt-BR";
        public int RadiusKm { get; set; } = 50;
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
    }
}