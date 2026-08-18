public class JobPosting
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsExactLocation { get; set; }
        
        // 🔹 Campos essenciais para o controle de cache:
        public string SearchQuery { get; set; } = string.Empty;
        public string SearchCity { get; set; } = string.Empty;
        public string SourceProvider { get; set; } = string.Empty; // "Adzuna", "Jooble", "JSearch"
        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }