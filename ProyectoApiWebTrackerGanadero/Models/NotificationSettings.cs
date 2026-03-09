using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class NotificationSettings
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // Informacion de contacto para notificaciones
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        public string? NotificationEmail { get; set; }

        // --- Canales de notificacion ---
        public bool EnableEmailNotifications { get; set; } = false;
        public bool EnableWhatsAppNotifications { get; set; } = false;

        // --- Alertas de Conectividad ---
        public bool AlertNoSignal { get; set; } = true;
        public bool AlertWeakSignal { get; set; } = false;
        public bool AlertAbruptDisconnection { get; set; } = true;

        // --- Alertas de Seguridad ---
        public bool AlertNightMovement { get; set; } = true;
        public bool AlertSuddenExit { get; set; } = true;
        public bool AlertUnusualSpeed { get; set; } = true;
        public bool AlertTrackerManipulation { get; set; } = true;

        // --- Alertas de Ubicacion y Geofencing ---
        public bool AlertOutOfBounds { get; set; } = true;
        public bool AlertImmobility { get; set; } = true;

        // --- Alertas de Actividad ---
        public bool AlertLowActivity { get; set; } = true;
        public bool AlertHighActivity { get; set; } = true;
        public bool AlertPossibleHeat { get; set; } = true;

        // --- Alertas de Bateria ---
        public bool AlertBatteryLow { get; set; } = true;
        public bool AlertBatteryCritical { get; set; } = true;

        // --- Alertas de Hardware / Precision ---
        public bool AlertInvalidCoordinates { get; set; } = false;
        public bool AlertLocationJump { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
