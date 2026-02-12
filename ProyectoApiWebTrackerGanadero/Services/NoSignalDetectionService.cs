using ApiWebTrackerGanado.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ApiWebTrackerGanado.Hubs;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Models;
using Microsoft.Extensions.DependencyInjection; // For IServiceScopeFactory

namespace ApiWebTrackerGanado.Services
{
    public class NoSignalDetectionService : BackgroundService
    {
        private readonly ILogger<NoSignalDetectionService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private const int NO_SIGNAL_THRESHOLD_MINUTES = 5; // Keep consistent with TrackingService

        public NoSignalDetectionService(ILogger<NoSignalDetectionService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("No Signal Detection Service running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("No Signal Detection Service checking trackers...");

                try
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var trackerRepository = scope.ServiceProvider.GetRequiredService<ITrackerRepository>();
                        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<LiveTrackingHub>>();
                        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

                        // Get all trackers including their associated animal for alerts and SignalR groups
                        var trackers = await trackerRepository.GetAllTrackersWithAnimalsAsync();

                        foreach (var tracker in trackers)
                        {
                            if (tracker.Animal == null) continue; // Only process trackers assigned to animals

                            var timeSinceLastSeen = DateTime.UtcNow - tracker.LastSeen;
                            bool currentlyHasSignal = timeSinceLastSeen.TotalMinutes <= NO_SIGNAL_THRESHOLD_MINUTES;

                            if (!currentlyHasSignal && tracker.IsOnline)
                            {
                                // Tracker has gone offline
                                _logger.LogWarning($"Tracker {tracker.DeviceId} (Animal {tracker.Animal.Id}) detected as offline (LastSeen: {tracker.LastSeen}).");
                                tracker.IsOnline = false;
                                await trackerRepository.UpdateAsync(tracker); // Update in DB

                                // Send SignalR update for no signal
                                var noSignalLocationDto = new LocationDto
                                {
                                    Latitude = 0.0, // Placeholder when no signal
                                    Longitude = 0.0, // Placeholder
                                    Altitude = 0.0,
                                    Speed = 0.0,
                                    ActivityLevel = 0,
                                    Temperature = 0.0,
                                    Timestamp = tracker.LastSeen, // Show last seen time
                                    HasSignal = false
                                };

                                await hubContext.Clients.Group($"animal_{tracker.Animal.Id}")
                                    .SendAsync("LocationUpdate", tracker.Animal.Id, noSignalLocationDto);
                                await hubContext.Clients.Group($"farm_{tracker.Animal.FarmId}")
                                    .SendAsync("AnimalLocationUpdate", tracker.Animal.Id, noSignalLocationDto);

                                // Trigger no signal alert
                                await alertService.TriggerNoSignalAlertAsync(tracker.Animal.Id, tracker.Id, "Tracker sin señal: No se han recibido datos recientes.");
                            }
                            else if (currentlyHasSignal && !tracker.IsOnline)
                            {
                                // Tracker is back online
                                _logger.LogInformation($"Tracker {tracker.DeviceId} (Animal {tracker.Animal.Id}) is back online (LastSeen: {tracker.LastSeen}).");
                                tracker.IsOnline = true;
                                await trackerRepository.UpdateAsync(tracker); // Update in DB

                                // Send SignalR update for back online
                                // For accurate position, the client should re-fetch if needed.
                                // We send a simplified update indicating it's online.
                                var backOnlineLocationDto = new LocationDto
                                {
                                    Latitude = 0.0, // The client will likely get the actual last location from a separate API call
                                    Longitude = 0.0,
                                    Altitude = 0.0,
                                    Speed = 0.0,
                                    ActivityLevel = 0,
                                    Temperature = 0.0,
                                    Timestamp = tracker.LastSeen, // Use LastSeen as it's the most recent known data point
                                    HasSignal = true
                                };

                                await hubContext.Clients.Group($"animal_{tracker.Animal.Id}")
                                    .SendAsync("LocationUpdate", tracker.Animal.Id, backOnlineLocationDto);
                                await hubContext.Clients.Group($"farm_{tracker.Animal.FarmId}")
                                    .SendAsync("AnimalLocationUpdate", tracker.Animal.Id, backOnlineLocationDto);

                                // Resolve no signal alert
                                await alertService.ResolveNoSignalAlertAsync(tracker.Animal.Id, tracker.Id, "El tracker ha recuperado la señal.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in No Signal Detection Service.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Check every 1 minute
            }

            _logger.LogInformation("No Signal Detection Service stopped.");
        }
    }
}
