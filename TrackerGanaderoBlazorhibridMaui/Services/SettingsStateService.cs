
namespace TrackerGanaderoBlazorHibridMaui.Services
{
    public class SettingsStateService
    {
        // Estado del tab activo
        public string ActiveTab { get; set; } = "license";

        // Estado de los trackers
        public List<TrackerDiscoveryDto>? ScannedTrackers { get; set; }
        public List<CustomerTrackerDto>? MyTrackers { get; set; }
        public List<TrackerDiscoveryDto>? AvailableTrackers { get; set; }

        // Estado de la licencia
        public CustomerInfoDto? CustomerInfo { get; set; }

        // Mensajes y flags
        public string? LastTrackerMessage { get; set; }
        public bool LastTrackerSuccess { get; set; }

        // Timestamp de último escaneo para determinar si datos son válidos
        public DateTime? LastScanTime { get; set; }

        // Método para limpiar estado cuando sea necesario
        public void ClearState()
        {
            ActiveTab = "license";
            ScannedTrackers = null;
            MyTrackers = null;
            AvailableTrackers = null;
            CustomerInfo = null;
            LastTrackerMessage = null;
            LastTrackerSuccess = false;
            LastScanTime = null;
        }

        // Método para determinar si los datos escaneados son recientes (válidos por 30 minutos)
        public bool HasRecentScanData()
        {
            return LastScanTime.HasValue &&
                   ScannedTrackers?.Any() == true &&
                   DateTime.UtcNow.Subtract(LastScanTime.Value).TotalMinutes < 30;
        }

        // Método para actualizar el estado después de un escaneo
        public void UpdateScanState(List<TrackerDiscoveryDto> trackers, string? message = null, bool success = true)
        {
            ScannedTrackers = trackers;
            LastScanTime = DateTime.UtcNow;
            LastTrackerMessage = message;
            LastTrackerSuccess = success;

            // NO cambiar automáticamente el tab - respetar la selección del usuario
            // El usuario puede estar trabajando en otros tabs como "assignment"
        }
    }
}