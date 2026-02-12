using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class SaveLocationHistoryDto
    {
        [Required]
        public int AnimalId { get; set; }

        [Required]
        public int TrackerId { get; set; }

        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }

        public double Altitude { get; set; } = 0;

        [Required]
        [Range(0, double.MaxValue)]
        public double Speed { get; set; }

        [Range(0, 100)]
        public int ActivityLevel { get; set; } = 50;

        public double Temperature { get; set; } = 0;

        [Range(0, 100)]
        public int SignalStrength { get; set; } = 100;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string? Source { get; set; } = "Frontend"; // Para identificar que viene del frontend
    }
}