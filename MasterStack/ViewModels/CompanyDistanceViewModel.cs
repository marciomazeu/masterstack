namespace MasterStack.ViewModels
{
    public class CompanyDistanceViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? City { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceInKm { get; set; }
    }
}