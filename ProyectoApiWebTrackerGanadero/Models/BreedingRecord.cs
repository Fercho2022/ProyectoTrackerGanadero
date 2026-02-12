using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class BreedingRecord
    {
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        [StringLength(20)]
        public string EventType { get; set; } = string.Empty;

        public DateTime EventDate { get; set; }

        public DateTime? ExpectedBirthDate { get; set; }

        public DateTime? ActualBirthDate { get; set; }

        public int? OffspringCount { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
