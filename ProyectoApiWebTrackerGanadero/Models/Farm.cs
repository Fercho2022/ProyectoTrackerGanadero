using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ApiWebTrackerGanado.Models
{
    public class Farm
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Address { get; set; }

        // Farm coordinates
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Farm boundary coordinates stored in separate table
        public ICollection<FarmBoundary> BoundaryCoordinates { get; set; } = new List<FarmBoundary>();

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public ICollection<Animal> Animals { get; set; } = new List<Animal>();
        public ICollection<Pasture> Pastures { get; set; } = new List<Pasture>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

