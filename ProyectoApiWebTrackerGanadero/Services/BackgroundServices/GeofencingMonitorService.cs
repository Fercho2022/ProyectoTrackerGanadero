using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Helpers;

namespace ApiWebTrackerGanado.Services.BackgroundServices
{
    /// <summary>
    /// Background service that monitors geofencing alerts and automatically resolves them
    /// when animals return to the geofenced area
    /// </summary>
    public class GeofencingMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GeofencingMonitorService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2); // Check every 2 minutes

        public GeofencingMonitorService(
            IServiceProvider serviceProvider,
            ILogger<GeofencingMonitorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔍 GeofencingMonitorService started - checking for auto-resolution every {Interval} minutes",
                _checkInterval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndResolveGeofencingAlerts();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in GeofencingMonitorService");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CheckAndResolveGeofencingAlerts()
        {
            using var scope = _serviceProvider.CreateScope();

            try
            {
                var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                var animalRepository = scope.ServiceProvider.GetRequiredService<IAnimalRepository>();
                var locationHistoryRepository = scope.ServiceProvider.GetRequiredService<ILocationHistoryRepository>();

                // Get all active "OutOfBounds" alerts
                var activeGeofencingAlerts = await alertService.GetActiveGeofencingAlertsAsync();

                if (!activeGeofencingAlerts.Any())
                {
                    return; // No active geofencing alerts to process
                }

                _logger.LogInformation("🔍 Checking {Count} active geofencing alerts for auto-resolution",
                    activeGeofencingAlerts.Count());

                foreach (var alert in activeGeofencingAlerts)
                {
                    try
                    {
                        await ProcessGeofencingAlert(alert, animalRepository, locationHistoryRepository, alertService);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error processing alert {AlertId} for animal {AnimalId}",
                            alert.Id, alert.AnimalId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in CheckAndResolveGeofencingAlerts");
            }
        }

        private async Task ProcessGeofencingAlert(
            ApiWebTrackerGanado.Models.Alert alert,
            IAnimalRepository animalRepository,
            ILocationHistoryRepository locationHistoryRepository,
            IAlertService alertService)
        {
            // Get animal with farm boundary information
            var animal = await animalRepository.GetByIdWithLocationAsync(alert.AnimalId);

            if (animal?.Farm?.BoundaryCoordinates == null || !animal.Farm.BoundaryCoordinates.Any())
            {
                _logger.LogWarning("⚠️ Animal {AnimalId} ({AnimalName}) has no geofencing boundaries defined",
                    alert.AnimalId, animal?.Name ?? "Unknown");
                return;
            }

            // Get the animal's latest location within the last 10 minutes
            var latestLocation = await locationHistoryRepository.GetLatestLocationAsync(
                alert.AnimalId,
                TimeSpan.FromMinutes(10));

            if (latestLocation == null)
            {
                _logger.LogDebug("📍 No recent location data for animal {AnimalId} ({AnimalName})",
                    alert.AnimalId, animal.Name);
                return;
            }

            // Check if the animal is now inside the geofenced area
            bool isInsideArea = GeofencingHelper.IsPointInPolygon(
                latestLocation.Latitude,
                latestLocation.Longitude,
                animal.Farm.BoundaryCoordinates);

            if (isInsideArea)
            {
                // Animal is back inside - resolve the alert automatically
                await alertService.ResolveAlertAsync(alert.Id, "Auto-resolved: Animal returned to geofenced area");

                _logger.LogInformation("✅ Auto-resolved geofencing alert for {AnimalName} (Alert #{AlertId}) - animal returned to area at {Timestamp}",
                    animal.Name, alert.Id, latestLocation.Timestamp);
            }
            else
            {
                _logger.LogDebug("📍 Animal {AnimalName} still outside geofenced area (lat: {Lat}, lng: {Lng})",
                    animal.Name, latestLocation.Latitude, latestLocation.Longitude);
            }
        }
    }
}