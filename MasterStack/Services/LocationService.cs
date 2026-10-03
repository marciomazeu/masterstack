using System.Text.Json;
using MasterStack.Models;

namespace MasterStack.Services
{
    public interface ILocationService
    {
        Task<List<CountryDto>> GetCountriesAsync();
        Task<List<CountryDto>> GetCountriesForCulture(string culture);
        Task<List<string>> GetCitiesByCountryAsync(string countryCode, string? state);
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

            try
            {
                var filePath = Path.Combine(_env.ContentRootPath, "Data", "countries-cities.json");
                
                if (!File.Exists(filePath))
                {
                    // Fallback para procurar no diretório do binário compilado caso não esteja no ContentRootPath
                    filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "countries-cities.json");
                }

                if (!File.Exists(filePath))
                {
                    _cachedLocations = new List<CountryDto>();
                    return _cachedLocations;
                }

                var json = await File.ReadAllTextAsync(filePath);
                _cachedLocations = JsonSerializer.Deserialize<List<CountryDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<CountryDto>();

                return _cachedLocations;
            }
            catch
            {
                _cachedLocations = new List<CountryDto>();
                return _cachedLocations;
            }
        }

        public async Task<List<CountryDto>> GetCountriesAsync()
        {
            return await LoadLocationsAsync();
        }

        public async Task<List<CountryDto>> GetCountriesForCulture(string culture)
        {
            return await LoadLocationsAsync();
        }

        public async Task<List<string>> GetCitiesByCountryAsync(string countryCode, string? state)
        {
            if (string.IsNullOrWhiteSpace(countryCode)) return new List<string>();

            var locations = await LoadLocationsAsync();
            var country = locations.FirstOrDefault(c => c.Iso2.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
            return country?.Cities ?? new List<string>();
        }
    }
}