namespace MasterStack.DTOs
{
    public class CompanyDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OfficeType { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}