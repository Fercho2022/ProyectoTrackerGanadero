using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class TrackerDataDto
    {
        [Required]
        public string DeviceId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public int ActivityLevel { get; set; }
        public double Temperature { get; set; }
        public int BatteryLevel { get; set; }
        public int SignalStrength { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tipo de posicionamiento: GPS (real), LBS (torre celular, aproximado), WIFI, EMU (emulador)
        /// </summary>
        public string LocationType { get; set; } = "GPS";

        // customerId es opcional - viene del usuario logueado, NO del tracker GPS
        public int? CustomerId { get; set; }
    }
}
