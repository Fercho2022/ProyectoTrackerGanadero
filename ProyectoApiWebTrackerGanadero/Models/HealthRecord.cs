using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class HealthRecord
    {
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        [StringLength(50)]
        public string RecordType { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime RecordDate { get; set; }

        [StringLength(100)]
        public string? VeterinarianName { get; set; }

        [StringLength(500)]
        public string? Treatment { get; set; }

        [StringLength(100)]
        public string? Medication { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime? NextCheckup { get; set; }

        public decimal? Cost { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
