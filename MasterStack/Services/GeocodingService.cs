using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MasterStack.Services
{
    public class GeocodingService : IGeocodingService
    {
        private readonly HttpClient _httpClient;

        public GeocodingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // O Nominatim do OpenStreetMap exige um User-Agent identificado
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MasterStackApp/1.0 (contact@masterstack.com)");
        }

        public async Task<(double? Lat, double? Lon)> GetCoordinatesAsync(string address, string city, string countryCode, string postalCode)
        {
            try
            {
                // Limpa e formata os parâmetros
                string cleanCountry = (countryCode ?? "").Trim().ToLower();
                string cleanCity = (city ?? "").Trim();
                string cleanPostal = (postalCode ?? "").Trim();
                string cleanAddress = (address ?? "").Trim();

                // 1. TENTATIVA 1: Busca Estruturada (Muito mais precisa)
                // O parâmetro countrycodes FORÇA a busca a ser estritamente no país selecionado
                var urlBuilder = new System.Text.StringBuilder("https://nominatim.openstreetmap.org/search?format=json&limit=1");

                if (!string.IsNullOrWhiteSpace(cleanCountry))
                    urlBuilder.Append($"&countrycodes={Uri.EscapeDataString(cleanCountry)}");

                if (!string.IsNullOrWhiteSpace(cleanPostal))
                    urlBuilder.Append($"&postalcode={Uri.EscapeDataString(cleanPostal)}");

                if (!string.IsNullOrWhiteSpace(cleanCity))
                    urlBuilder.Append($"&city={Uri.EscapeDataString(cleanCity)}");

                if (!string.IsNullOrWhiteSpace(cleanAddress))
                    urlBuilder.Append($"&street={Uri.EscapeDataString(cleanAddress)}");

                var coords = await ExecuteNominatimQueryAsync(urlBuilder.ToString());

                // 2. TENTATIVA 2 (FALLBACK): Se o CEP/Endereço falhou, tenta APENAS Cidade + País
                if (coords == (null, null) && !string.IsNullOrWhiteSpace(cleanCity))
                {
                    var fallbackUrl = $"https://nominatim.openstreetmap.org/search?city={Uri.EscapeDataString(cleanCity)}&countrycodes={Uri.EscapeDataString(cleanCountry)}&format=json&limit=1";
                    coords = await ExecuteNominatimQueryAsync(fallbackUrl);
                }

                return coords;
            }
            catch
            {
                // Em caso de falha de rede/API, ignora sem quebrar a aplicação
                return (null, null);
            }
        }

       // Método auxiliar interno para evitar repetição de código HTTP
        private async Task<(double? Lat, double? Lon)> ExecuteNominatimQueryAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (null, null);

            var json = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);

            if (results != null && results.Count > 0)
            {
                if (double.TryParse(results[0].Lat, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(results[0].Lon, NumberStyles.Any, CultureInfo.InvariantCulture, out var lon))
                {
                    return (lat, lon);
                }
            }

            return (null, null);
        }

        // Fórmula de Haversine para calcular distância entre duas coordenadas em KM
        public double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Raio da Terra em KM
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double val) => (Math.PI / 180) * val;

        private class NominatimResult
        {
            [JsonPropertyName("lat")]
            public string Lat { get; set; } = string.Empty;

            [JsonPropertyName("lon")]
            public string Lon { get; set; } = string.Empty;
        }
    }
}