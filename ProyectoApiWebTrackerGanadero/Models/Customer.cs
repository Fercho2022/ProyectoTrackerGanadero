using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    /// <summary>
    /// Modelo que extiende User para agregar información comercial del cliente
    /// </summary>
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? TaxId { get; set; } // RUT, CUIT, etc.

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        /// <summary>
        /// Plan contratado (Basic, Premium, Enterprise)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Plan { get; set; } = "Basic";

        /// <summary>
        /// Límite máximo de trackers según el plan
        /// </summary>
        public int TrackerLimit { get; set; } = 10;

        /// <summary>
        /// Límite máximo de granjas según el plan
        /// </summary>
        public int FarmLimit { get; set; } = 1;

        /// <summary>
        /// Estado del cliente (Active, Suspended, Expired)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active";

        public DateTime SubscriptionStart { get; set; } = DateTime.UtcNow;
        public DateTime? SubscriptionEnd { get; set; }

        // Relaciones
        public ICollection<License> Licenses { get; set; } = new List<License>();
        public ICollection<CustomerTracker> CustomerTrackers { get; set; } = new List<CustomerTracker>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Verifica si el cliente puede agregar más trackers
        /// </summary>
        public bool CanAddMoreTrackers()
        {
            return Status == "Active" && CustomerTrackers.Count < TrackerLimit;
        }

        /// <summary>
        /// Verifica si la suscripción está vigente
        /// </summary>
        public bool IsSubscriptionActive()
        {
            return Status == "Active" &&
                   (SubscriptionEnd == null || SubscriptionEnd > DateTime.UtcNow);
        }
    }
}