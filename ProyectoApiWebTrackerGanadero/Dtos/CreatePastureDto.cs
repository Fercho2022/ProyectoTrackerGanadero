using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class CreatePastureDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public List<LatLngDto> AreaCoordinates { get; set; } = new();
        public string GrassType { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public int FarmId { get; set; }
    }
}
