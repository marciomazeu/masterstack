using System.Text.Json;
using MasterStack.Models;

namespace MasterStack.Services
{
    public interface ILocationService
    {
        Task<List<CountryDto>> GetCountriesAsync();
        
        // 📌 Método adicionado para resolver o erro
        Task<List<CountryDto>> GetCountriesForCulture(string culture);
        
        Task<List<string>> GetCitiesByCountryAsync(string countryCode);
    }

    public class CountryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Iso2 { get; set; } = string.Empty;
        public List<string> Cities { get; set; } = new();
    }

    public class LocationService : ILocationService
    {
        private readonly IWebHostEnvironment _env;
        private List<CountryDto>? _cachedLocations;

        public LocationService(IWebHostEnvironment env)
        {
            _env = env;
        }

        private async Task<List<CountryDto>> LoadLocationsAsync()
        {
            if (_cachedLocations != null) return _cachedLocations;

            var filePath = Path.Combine(_env.ContentRootPath, "Data", "countries-cities.json");
            if (!File.Exists(filePath))
            {
                return new List<CountryDto>();
            }

            var json = await File.ReadAllTextAsync(filePath);
            _cachedLocations = JsonSerializer.Deserialize<List<CountryDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<CountryDto>();

            return _cachedLocations;
        }

        public async Task<List<CountryDto>> GetCountriesAsync()
        {
            return await LoadLocationsAsync();
        }

        // 📌 Implementação do GetCountriesForCulture
        public async Task<List<CountryDto>> GetCountriesForCulture(string culture)
        {
            // Retorna os países da base. Se precisar traduzir nomes de países 
            // no futuro com base na culture ("pt-BR", "en-US"), a lógica entra aqui.
            return await LoadLocationsAsync();
        }

        public async Task<List<string>> GetCitiesByCountryAsync(string countryCode)
        {
            var locations = await LoadLocationsAsync();
            var country = locations.FirstOrDefault(c => c.Iso2.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
            return country?.Cities ?? new List<string>();
        }
    }
}