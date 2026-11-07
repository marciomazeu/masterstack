public interface ILinkedInService
{
    Task<bool> SharePostAsync(string textContent, string articleUrl);
}