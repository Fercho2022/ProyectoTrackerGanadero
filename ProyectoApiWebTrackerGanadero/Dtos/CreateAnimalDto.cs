using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class CreateAnimalDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Tag { get; set; }
        public DateTime BirthDate { get; set; }
        [Required]
        public string Gender { get; set; } = string.Empty;
        [Required]
        public string Breed { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string Status { get; set; } = "Active";
        public int FarmId { get; set; }
        public int? TrackerId { get; set; }
    }
}
