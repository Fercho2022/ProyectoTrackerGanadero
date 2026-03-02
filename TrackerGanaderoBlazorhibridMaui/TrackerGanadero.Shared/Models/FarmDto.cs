namespace TrackerGanadero.Shared.Models
{
    public class FarmDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<LatLngDto> BoundaryCoordinates { get; set; } = new();
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class LatLngDto
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}