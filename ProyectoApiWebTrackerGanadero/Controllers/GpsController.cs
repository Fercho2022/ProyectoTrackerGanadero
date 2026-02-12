using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;
using ApiWebTrackerGanado.Services;
using ApiWebTrackerGanado.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GpsController : ControllerBase
    {
        private readonly CattleTrackingContext _context;
        private readonly TrackerDiscoveryService _trackerDiscoveryService;
        private readonly IAlertService _alertService;
        private readonly ILogger<GpsController> _logger;

        public GpsController(
            CattleTrackingContext context,
            TrackerDiscoveryService trackerDiscoveryService,
            IAlertService alertService,
            ILogger<GpsController> logger)
        {
            _context = context;
            _trackerDiscoveryService = trackerDiscoveryService;
            _alertService = alertService;
            _logger = logger;
        }

        /// <summary>
        /// Endpoint para recibir actualizaciones GPS de trackers en tiempo real
        /// </summary>
        [HttpPost("update")]
        public async Task<IActionResult> ReceiveGpsUpdate([FromBody] GpsUpdateRequest request)
        {
            try
            {
                _logger.LogInformation($"📡 GPS Update received from {request.DeviceId}");

                // Validar datos requeridos
                if (string.IsNullOrWhiteSpace(request.DeviceId))
                {
                    return BadRequest(new { success = false, message = "DeviceId is required" });
                }

                // 1. Buscar o crear el tracker
                var tracker = await _context.Trackers
                    .FirstOrDefaultAsync(t => t.DeviceId == request.DeviceId);

                if (tracker == null)
                {
                    _logger.LogInformation($"🆕 New tracker discovered: {request.DeviceId}");

                    // Auto-descubrir y registrar nuevo tracker
                    var newTracker = new Tracker
                    {
                        DeviceId = request.DeviceId,
                        Name = $"Tracker {request.DeviceId}",
                        SerialNumber = request.SerialNumber ?? request.DeviceId,
                        Manufacturer = request.Manufacturer ?? "Unknown",
                        Model = request.Model ?? "Unknown",
                        Status = "Discovered", // Estado inicial: descubierto pero no asignado
                        IsActive = true,
                        IsOnline = true,
                        IsAvailableForAssignment = true,
                        BatteryLevel = (int)(request.BatteryLevel ?? 100),
                        LastSeen = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Trackers.Add(newTracker);
                    await _context.SaveChangesAsync();

                    tracker = newTracker;
                    _logger.LogInformation($"✅ Tracker {request.DeviceId} auto-registered with ID {tracker.Id}");
                }
                else
                {
                    // Actualizar información del tracker existente
                    tracker.IsOnline = true;
                    tracker.LastSeen = DateTime.UtcNow;
                    tracker.BatteryLevel = (int)(request.BatteryLevel ?? tracker.BatteryLevel);

                    // Actualizar información técnica si está disponible
                    if (!string.IsNullOrEmpty(request.SerialNumber))
                        tracker.SerialNumber = request.SerialNumber;
                    if (!string.IsNullOrEmpty(request.Manufacturer))
                        tracker.Manufacturer = request.Manufacturer;
                    if (!string.IsNullOrEmpty(request.Model))
                        tracker.Model = request.Model;

                    _logger.LogInformation($"🔄 Updated tracker {request.DeviceId} (ID: {tracker.Id})");
                }

                // 2. Guardar datos de ubicación en LocationHistory
                // IMPORTANTE: Solo si el tracker está en estado "Active" (asignado a un customer)
                if (tracker.Status == "Active")
                {
                    // CRÍTICO: Buscar el animal asociado a este tracker
                    var animal = await _context.Animals
                        .FirstOrDefaultAsync(a => a.TrackerId == tracker.Id);

                    if (animal != null)
                    {
                        // Asegurar que el timestamp sea UTC para PostgreSQL
                        var timestamp = request.Timestamp.HasValue
                            ? (request.Timestamp.Value.Kind == DateTimeKind.Utc
                                ? request.Timestamp.Value
                                : request.Timestamp.Value.ToUniversalTime())
                            : DateTime.UtcNow;

                        var locationHistory = new LocationHistory
                        {
                            AnimalId = animal.Id, // CRÍTICO: Agregar AnimalId para que las consultas funcionen
                            TrackerId = tracker.Id,
                            DeviceId = request.DeviceId,
                            Latitude = request.Latitude,
                            Longitude = request.Longitude,
                            Altitude = request.Altitude ?? 0,
                            Speed = request.Speed ?? 0,
                            Timestamp = timestamp,
                            ActivityLevel = CalculateActivityLevel(request.Speed ?? 0),
                            Temperature = request.InternalTemperature ?? 0,
                            SignalStrength = request.SignalStrength ?? 0
                        };

                        _context.LocationHistories.Add(locationHistory);
                        _logger.LogInformation($"📍 Location saved for {request.DeviceId} (Animal: {animal.Name}): ({request.Latitude}, {request.Longitude})");

                        // IMPORTANTE: Verificar alertas de geofencing y otras alertas después de guardar la ubicación
                        await _context.SaveChangesAsync(); // Guardar primero para que locationHistory tenga ID

                        try
                        {
                            // Recargar el animal con la granja incluida para verificación de límites
                            var animalWithFarm = await _context.Animals
                                .Include(a => a.Farm)
                                .FirstOrDefaultAsync(a => a.Id == animal.Id);

                            if (animalWithFarm != null)
                            {
                                // Verificar alertas de geofencing (OutOfBounds)
                                await _alertService.CheckLocationAlertsAsync(animalWithFarm, locationHistory);

                                // Verificar alertas de actividad
                                await _alertService.CheckActivityAlertsAsync(animalWithFarm, locationHistory.ActivityLevel);

                                // Verificar alertas de seguridad
                                await _alertService.CheckSecurityAlertsAsync(animalWithFarm, locationHistory);

                                // Verificar salud del tracker
                                await _alertService.CheckTrackerHealthAlertsAsync(animalWithFarm, locationHistory, tracker);

                                _logger.LogDebug($"✅ Alert checks completed for {request.DeviceId}");
                            }
                        }
                        catch (Exception alertEx)
                        {
                            _logger.LogError(alertEx, $"⚠️ Error checking alerts for {request.DeviceId}");
                            // No fallar el request completo si fallan las alertas
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ Tracker {request.DeviceId} is Active but not assigned to any animal");
                    }
                }
                else
                {
                    _logger.LogInformation($"⏸️ Tracker {request.DeviceId} is in '{tracker.Status}' state - location not saved to history");
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "GPS data received successfully",
                    trackerId = tracker.Id,
                    deviceId = request.DeviceId,
                    status = tracker.Status,
                    locationSaved = tracker.Status == "Active",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error processing GPS update from {request.DeviceId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error processing GPS data",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Calcula el nivel de actividad basado en la velocidad
        /// </summary>
        private int CalculateActivityLevel(double speed)
        {
            // Escala de 0-10 basada en velocidad (km/h)
            if (speed < 0.5) return 1; // Inmóvil
            if (speed < 1.5) return 3; // Baja actividad
            if (speed < 3.0) return 5; // Actividad media
            if (speed < 5.0) return 7; // Alta actividad
            return 9; // Muy alta actividad
        }

        /// <summary>
        /// Endpoint de prueba para verificar que el servicio GPS está activo
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                success = true,
                message = "GPS service is running",
                timestamp = DateTime.UtcNow,
                endpoint = "/api/gps/update"
            });
        }

        /// <summary>
        /// Obtiene estadísticas de trackers activos
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var totalTrackers = await _context.Trackers.CountAsync();
                var onlineTrackers = await _context.Trackers.CountAsync(t => t.IsOnline);
                var activeTrackers = await _context.Trackers.CountAsync(t => t.Status == "Active");
                var discoveredTrackers = await _context.Trackers.CountAsync(t => t.Status == "Discovered");

                var recentLocations = await _context.LocationHistories
                    .Where(lh => lh.Timestamp > DateTime.UtcNow.AddMinutes(-5))
                    .CountAsync();

                return Ok(new
                {
                    success = true,
                    stats = new
                    {
                        totalTrackers,
                        onlineTrackers,
                        activeTrackers,
                        discoveredTrackers,
                        recentLocations_last5min = recentLocations
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GPS stats");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Modelo para recibir actualizaciones GPS de trackers
    /// </summary>
    public class GpsUpdateRequest
    {
        // Identificación del dispositivo
        public string DeviceId { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? FirmwareVersion { get; set; }
        public string? HardwareVersion { get; set; }

        // Datos GPS principales
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime? Timestamp { get; set; }
        public double? Altitude { get; set; }

        // Datos de movimiento
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public double? SpeedAccuracy { get; set; }

        // Calidad de señal GPS
        public double? Accuracy { get; set; }
        public double? HorizontalAccuracy { get; set; }
        public double? VerticalAccuracy { get; set; }
        public double? Hdop { get; set; }
        public int? Satellites { get; set; }
        public string? GpsFixQuality { get; set; }

        // Estado de la batería
        public double? BatteryLevel { get; set; }
        public double? BatteryVoltage { get; set; }
        public string? ChargingStatus { get; set; }
        public double? BatteryTemperature { get; set; }

        // Red celular
        public int? SignalStrength { get; set; }
        public string? NetworkType { get; set; }
        public string? Operator { get; set; }
        public int? Mcc { get; set; }
        public int? Mnc { get; set; }
        public int? Lac { get; set; }
        public int? CellId { get; set; }

        // Sensores del dispositivo
        public double? InternalTemperature { get; set; }
        public double? AccelerometerX { get; set; }
        public double? AccelerometerY { get; set; }
        public double? AccelerometerZ { get; set; }

        // Configuración del tracker
        public int? TransmissionInterval { get; set; }
        public int? TransmissionPower { get; set; }
        public string? GpsModule { get; set; }
        public string? ModemModule { get; set; }
    }
}
