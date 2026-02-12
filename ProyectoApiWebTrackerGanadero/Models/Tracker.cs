using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class Tracker
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string DeviceId { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string Model { get; set; } = string.Empty;

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        public int BatteryLevel { get; set; } = 100;

        public bool IsActive { get; set; } = true;

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public bool IsOnline { get; set; } = true;

        /// <summary>
        /// Indica si el tracker está disponible para asignación
        /// </summary>
        public bool IsAvailableForAssignment { get; set; } = true;

        /// <summary>
        /// Número de serie del dispositivo físico
        /// </summary>
        [StringLength(100)]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Manufacturer del tracker
        /// </summary>
        [StringLength(100)]
        public string? Manufacturer { get; set; }

        // Relaciones existentes
        public Animal? Animal { get; set; }
        public ICollection<LocationHistory> LocationHistories { get; set; } = new List<LocationHistory>();

        // Nuevas relaciones para gestión de clientes
        public ICollection<CustomerTracker> CustomerTrackers { get; set; } = new List<CustomerTracker>();

        public int? UserId { get; set; } // Foreign key for User
        public User? User { get; set; } // Navigation property to User

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Verifica si el tracker está asignado a algún cliente
        /// </summary>
        public bool IsAssignedToCustomer()
        {
            return CustomerTrackers.Any(ct => ct.IsActive());
        }

        /// <summary>
        /// Obtiene el cliente actual del tracker
        /// </summary>
        public Customer? GetCurrentCustomer()
        {
            return CustomerTrackers
                .Where(ct => ct.IsActive())
                .Select(ct => ct.Customer)
                .FirstOrDefault();
        }

        /// <summary>
        /// Verifica si el tracker puede ser asignado
        /// </summary>
        public bool CanBeAssigned()
        {
            return IsActive &&
                   IsAvailableForAssignment &&
                   !IsAssignedToCustomer();
        }
    }
}
