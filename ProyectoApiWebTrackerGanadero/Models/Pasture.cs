using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class Pasture
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Temporarily disabled for PostGIS migration
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public Polygon Area { get; set; } = null!;

        public double AreaSize { get; set; }

        [StringLength(50)]
        public string GrassType { get; set; } = string.Empty;

        public int Capacity { get; set; }

        public bool IsActive { get; set; } = true;

        public int FarmId { get; set; }
        public Farm Farm { get; set; } = null!;

        public ICollection<PastureUsage> PastureUsages { get; set; } = new List<PastureUsage>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
