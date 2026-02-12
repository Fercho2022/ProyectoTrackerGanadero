using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class FarmBoundary
    {
        public int Id { get; set; }

        [Required]
        public int FarmId { get; set; }

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        [Required]
        public int SequenceOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Farm Farm { get; set; } = null!;
    }
}