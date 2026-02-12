using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class CreateFarmDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<LatLngDto>? Boundaries { get; set; }
        public List<LatLngDto>? BoundaryCoordinates { get; set; }
    }
}
