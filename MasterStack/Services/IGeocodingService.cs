namespace MasterStack.Services
{
    public interface IGeocodingService
    {
        Task<(double? Lat, double? Lon)> GetCoordinatesAsync(string address, string city, string countryCode, string postalCode);
        double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2);
    }
}