using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class WeightRecord
    {
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        public decimal Weight { get; set; }

        public DateTime WeightDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
