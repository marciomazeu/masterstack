namespace MasterStack.ViewModels;
public class ProfileViewModel
{
    public string DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? CurrentImageUrl { get; set; }
    public IFormFile? NewImage { get; set; } // Para o upload
    public string? TwitterUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GitHubUrl { get; set; }
}