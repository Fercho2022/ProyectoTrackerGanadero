using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class NotificationSettingsDto
    {
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        [EmailAddress]
        public string? NotificationEmail { get; set; }

        // Canales
        public bool EnableEmailNotifications { get; set; }
        public bool EnableWhatsAppNotifications { get; set; }

        // Conectividad
        public bool AlertNoSignal { get; set; } = true;
        public bool AlertWeakSignal { get; set; }
        public bool AlertAbruptDisconnection { get; set; } = true;

        // Seguridad
        public bool AlertNightMovement { get; set; } = true;
        public bool AlertSuddenExit { get; set; } = true;
        public bool AlertUnusualSpeed { get; set; } = true;
        public bool AlertTrackerManipulation { get; set; } = true;

        // Ubicacion
        public bool AlertOutOfBounds { get; set; } = true;
        public bool AlertImmobility { get; set; } = true;

        // Actividad
        public bool AlertLowActivity { get; set; } = true;
        public bool AlertHighActivity { get; set; } = true;
        public bool AlertPossibleHeat { get; set; } = true;

        // Bateria
        public bool AlertBatteryLow { get; set; } = true;
        public bool AlertBatteryCritical { get; set; } = true;

        // Hardware
        public bool AlertInvalidCoordinates { get; set; }
        public bool AlertLocationJump { get; set; }
    }
}
