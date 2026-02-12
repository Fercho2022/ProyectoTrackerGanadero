using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ApiWebTrackerGanado.Models
{
    public class Animal
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Tag { get; set; }

        public DateTime BirthDate { get; set; }

        [StringLength(20)]
        public string Gender { get; set; } = string.Empty;

        [StringLength(50)]
        public string Breed { get; set; } = string.Empty;

        public decimal Weight { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        public int FarmId { get; set; }
        public Farm Farm { get; set; } = null!;

        public int? TrackerId { get; set; }
        public Tracker? Tracker { get; set; }

        /// <summary>
        /// Relación con el CustomerTracker asignado a este animal
        /// Conecta la gestión de clientes con la gestión de animales
        /// </summary>
        public int? CustomerTrackerId { get; set; }
        public CustomerTracker? CustomerTracker { get; set; }

        public ICollection<LocationHistory> LocationHistories { get; set; } = new List<LocationHistory>();
        public ICollection<HealthRecord> HealthRecords { get; set; } = new List<HealthRecord>();
        public ICollection<WeightRecord> WeightRecords { get; set; } = new List<WeightRecord>();
        public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
        public ICollection<BreedingRecord> BreedingRecords { get; set; } = new List<BreedingRecord>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
