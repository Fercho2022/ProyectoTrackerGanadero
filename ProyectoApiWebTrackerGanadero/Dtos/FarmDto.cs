namespace ApiWebTrackerGanado.Dtos
{
    public class FarmDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<LatLngDto>? BoundaryCoordinates { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
