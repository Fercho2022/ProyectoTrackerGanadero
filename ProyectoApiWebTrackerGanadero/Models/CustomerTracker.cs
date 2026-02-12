using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    /// <summary>
    /// Modelo para la relación exclusiva entre Cliente y Tracker
    /// Garantiza que cada tracker pertenezca a un solo cliente
    /// </summary>
    public class CustomerTracker
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        [Required]
        public int TrackerId { get; set; }
        public Tracker Tracker { get; set; } = null!;

        /// <summary>
        /// Cómo se asignó este tracker (Manual, AutoDiscovery, Imported)
        /// </summary>
        // [Required]
        // [StringLength(50)]
        // public string AssignmentMethod { get; set; } = "Manual";
        // NOTA: Columna comentada porque no existe en la base de datos actual

        /// <summary>
        /// Estado de la asignación (Active, Inactive, Transferred)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Fecha cuando se detectó por primera vez el tracker
        /// </summary>
        // public DateTime? FirstDetectedAt { get; set; }
        // NOTA: Columna comentada porque no existe en la base de datos actual

        /// <summary>
        /// Fecha cuando se asignó al cliente
        /// </summary>
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha cuando se desasignó (si aplica)
        /// </summary>
        public DateTime? UnassignedAt { get; set; }

        /// <summary>
        /// Usuario que realizó la asignación
        /// </summary>
        // public int? AssignedByUserId { get; set; }
        // public User? AssignedByUser { get; set; }
        // NOTA: Columnas comentadas porque no existen en la base de datos actual

        /// <summary>
        /// Licencia bajo la cual se asignó este tracker
        /// </summary>
        // public int? LicenseId { get; set; }
        // public License? License { get; set; }
        // NOTA: Columnas comentadas porque no existen en la base de datos actual

        /// <summary>
        /// Nombre personalizado del tracker para este cliente
        /// </summary>
        // [StringLength(100)]
        // public string? CustomName { get; set; }
        // NOTA: Columna comentada porque no existe en la base de datos actual

        /// <summary>
        /// Notas del cliente sobre este tracker
        /// </summary>
        // [StringLength(500)]
        // public string? Notes { get; set; }
        // NOTA: Columna comentada porque no existe en la base de datos actual

        /// <summary>
        /// Configuración específica del tracker en JSON
        /// Ejemplo: {"alertRange": 500, "updateInterval": 60}
        /// </summary>
        // [StringLength(2000)]
        // public string? Configuration { get; set; }
        // NOTA: Columna comentada porque no existe en la base de datos actual

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Animal al que está asignado este CustomerTracker (si aplica)
        /// Conecta la gestión de clientes con la gestión de animales
        /// </summary>
        public Animal? AssignedAnimal { get; set; }

        /// <summary>
        /// Verifica si la asignación está activa
        /// </summary>
        public bool IsActive()
        {
            return Status == "Active" && UnassignedAt == null;
        }

        /// <summary>
        /// Desasigna el tracker del cliente
        /// </summary>
        public void Unassign(int userId)
        {
            Status = "Inactive";
            UnassignedAt = DateTime.UtcNow;
            // AssignedByUserId = userId; // Comentado porque la columna no existe
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Reactiva la asignación del tracker
        /// </summary>
        public void Reactivate(int userId)
        {
            Status = "Active";
            UnassignedAt = null;
            // AssignedByUserId = userId; // Comentado porque la columna no existe
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Transfiere el tracker a otro cliente
        /// </summary>
        public void TransferTo(int newCustomerId, int transferredByUserId)
        {
            Status = "Transferred";
            UnassignedAt = DateTime.UtcNow;
            // AssignedByUserId = transferredByUserId; // Comentado porque la columna no existe
            UpdatedAt = DateTime.UtcNow;
        }
    }
}