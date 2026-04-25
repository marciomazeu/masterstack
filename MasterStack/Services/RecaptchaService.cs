public class RecaptchaService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public RecaptchaService(IConfiguration config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<bool> Verify(string token)
    {
        var secret = _config["Recaptcha:SecretKey"];
        var response = await _httpClient.PostAsync(
            $"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={token}", null);
        
        var json = await response.Content.ReadAsStringAsync();
        // Aqui você verifica se "success": true no JSON retornado
        return json.Contains("\"success\": true");
    }
}