using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class Alert
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Severity { get; set; } = string.Empty;

        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        public bool IsRead { get; set; } = false;
        public bool IsResolved { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }
}
