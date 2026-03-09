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
using ApiWebTrackerGanado.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Services
{
    public class NoSignalDetectionService : BackgroundService
    {
        private readonly ILogger<NoSignalDetectionService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private const int NO_SIGNAL_THRESHOLD_MINUTES = 5;
        private const double MASS_DISCONNECT_THRESHOLD_PERCENT = 0.5; // 50% de trackers caen = alerta masiva

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
                        var context = scope.ServiceProvider.GetRequiredService<CattleTrackingContext>();

                        var trackers = await trackerRepository.GetAllTrackersWithAnimalsAsync();

                        // Contadores para deteccion de caida masiva
                        int totalWithAnimals = 0;
                        int newlyOfflineCount = 0;
                        var newlyOfflineTrackers = new List<Tracker>();

                        foreach (var tracker in trackers)
                        {
                            if (tracker.Animal == null) continue;
                            totalWithAnimals++;

                            var timeSinceLastSeen = DateTime.UtcNow - tracker.LastSeen;
                            bool currentlyHasSignal = timeSinceLastSeen.TotalMinutes <= NO_SIGNAL_THRESHOLD_MINUTES;

                            if (!currentlyHasSignal && tracker.IsOnline)
                            {
                                // Tracker ha perdido senal
                                _logger.LogWarning("Tracker {DeviceId} (Animal {AnimalId}) detected as offline (LastSeen: {LastSeen}).",
                                    tracker.DeviceId, tracker.Animal.Id, tracker.LastSeen);
                                tracker.IsOnline = false;
                                await trackerRepository.UpdateAsync(tracker);

                                newlyOfflineCount++;
                                newlyOfflineTrackers.Add(tracker);

                                // Obtener ultima ubicacion real del animal
                                var lastLocation = await context.LocationHistories
                                    .Where(lh => lh.AnimalId == tracker.Animal.Id)
                                    .OrderByDescending(lh => lh.Timestamp)
                                    .FirstOrDefaultAsync();

                                // Enviar SignalR con coordenadas reales (no 0,0)
                                var noSignalLocationDto = new LocationDto
                                {
                                    Latitude = lastLocation?.Latitude ?? 0.0,
                                    Longitude = lastLocation?.Longitude ?? 0.0,
                                    Altitude = lastLocation?.Altitude ?? 0.0,
                                    Speed = 0.0,
                                    ActivityLevel = 0,
                                    Temperature = lastLocation?.Temperature ?? 0.0,
                                    Timestamp = tracker.LastSeen,
                                    HasSignal = false
                                };

                                await hubContext.Clients.Group($"animal_{tracker.Animal.Id}")
                                    .SendAsync("LocationUpdate", tracker.Animal.Id, noSignalLocationDto);
                                await hubContext.Clients.Group($"farm_{tracker.Animal.FarmId}")
                                    .SendAsync("AnimalLocationUpdate", tracker.Animal.Id, noSignalLocationDto);

                                // Alerta individual por tracker
                                await alertService.TriggerNoSignalAlertAsync(tracker.Animal.Id, tracker.Id,
                                    $"Tracker sin señal: No se han recibido datos en los últimos {NO_SIGNAL_THRESHOLD_MINUTES} minutos.");
                            }
                            else if (currentlyHasSignal && !tracker.IsOnline)
                            {
                                // Tracker recupero senal
                                _logger.LogInformation("Tracker {DeviceId} (Animal {AnimalId}) is back online (LastSeen: {LastSeen}).",
                                    tracker.DeviceId, tracker.Animal.Id, tracker.LastSeen);
                                tracker.IsOnline = true;
                                await trackerRepository.UpdateAsync(tracker);

                                // Obtener ubicacion real para el SignalR de reconexion
                                var lastLocation = await context.LocationHistories
                                    .Where(lh => lh.AnimalId == tracker.Animal.Id)
                                    .OrderByDescending(lh => lh.Timestamp)
                                    .FirstOrDefaultAsync();

                                var backOnlineLocationDto = new LocationDto
                                {
                                    Latitude = lastLocation?.Latitude ?? 0.0,
                                    Longitude = lastLocation?.Longitude ?? 0.0,
                                    Altitude = lastLocation?.Altitude ?? 0.0,
                                    Speed = lastLocation?.Speed ?? 0.0,
                                    ActivityLevel = lastLocation?.ActivityLevel ?? 0,
                                    Temperature = lastLocation?.Temperature ?? 0.0,
                                    Timestamp = tracker.LastSeen,
                                    HasSignal = true
                                };

                                await hubContext.Clients.Group($"animal_{tracker.Animal.Id}")
                                    .SendAsync("LocationUpdate", tracker.Animal.Id, backOnlineLocationDto);
                                await hubContext.Clients.Group($"farm_{tracker.Animal.FarmId}")
                                    .SendAsync("AnimalLocationUpdate", tracker.Animal.Id, backOnlineLocationDto);

                                await alertService.ResolveNoSignalAlertAsync(tracker.Animal.Id, tracker.Id,
                                    "El tracker ha recuperado la señal.");
                            }
                        }

                        // Deteccion de caida masiva del sistema de trackers
                        if (totalWithAnimals > 0 && newlyOfflineCount >= 3 &&
                            (double)newlyOfflineCount / totalWithAnimals >= MASS_DISCONNECT_THRESHOLD_PERCENT)
                        {
                            _logger.LogCritical(
                                "CAIDA MASIVA DETECTADA: {Count}/{Total} trackers perdieron señal simultaneamente.",
                                newlyOfflineCount, totalWithAnimals);

                            // Obtener las granjas afectadas para notificar a cada una
                            var affectedFarmIds = newlyOfflineTrackers
                                .Where(t => t.Animal != null)
                                .Select(t => t.Animal!.FarmId)
                                .Distinct();

                            foreach (var farmId in affectedFarmIds)
                            {
                                var trackersInFarm = newlyOfflineTrackers
                                    .Count(t => t.Animal?.FarmId == farmId);

                                // Broadcast alerta masiva via SignalR a la granja
                                await hubContext.Clients.Group($"farm_{farmId}")
                                    .SendAsync("MassDisconnectionAlert", new
                                    {
                                        FarmId = farmId,
                                        DisconnectedCount = trackersInFarm,
                                        TotalTrackers = totalWithAnimals,
                                        Timestamp = DateTime.UtcNow,
                                        Message = $"Caída masiva del sistema: {trackersInFarm} trackers perdieron señal simultáneamente. " +
                                                  $"Contacte al administrador del sistema."
                                    });

                                // Crear alerta critica de caida masiva usando el primer animal de la granja
                                var firstAnimalInFarm = newlyOfflineTrackers
                                    .FirstOrDefault(t => t.Animal?.FarmId == farmId)?.Animal;

                                if (firstAnimalInFarm != null)
                                {
                                    await alertService.CreateMassDisconnectionAlertAsync(
                                        firstAnimalInFarm.Id,
                                        $"CAÍDA MASIVA DEL SISTEMA: {trackersInFarm} trackers perdieron señal simultáneamente. " +
                                        $"Posible fallo en la infraestructura de comunicaciones. " +
                                        $"Contacte al administrador del sistema.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in No Signal Detection Service.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            _logger.LogInformation("No Signal Detection Service stopped.");
        }
    }
}
