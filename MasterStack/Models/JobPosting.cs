public class JobPosting
{
    public int Id { get; set; } // Chave primária autoincrementada (NÃO alterar no C#)

    public string UserId { get; set; } = string.Empty; // Chave estrangeira ligando ao AspNetUsers
    
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsExactLocation { get; set; }
}