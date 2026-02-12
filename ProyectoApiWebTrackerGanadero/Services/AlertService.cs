using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Helpers;
using ApiWebTrackerGanado.Hubs;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Services
{
    public class AlertService : IAlertService 
    {

        private readonly IAlertRepository _alertRepository;
        private readonly IFarmRepository _farmRepository;
        private readonly ILocationHistoryRepository _locationHistoryRepository;
        private readonly IHubContext<LiveTrackingHub> _hubContext;
        private readonly IAnimalRepository _animalRepository;

        public AlertService(
            IAlertRepository alertRepository,
            IFarmRepository farmRepository,
            ILocationHistoryRepository locationHistoryRepository,
            IHubContext<LiveTrackingHub> hubContext,
            IAnimalRepository animalRepository)
        {
            _alertRepository = alertRepository;
            _farmRepository = farmRepository;
            _locationHistoryRepository = locationHistoryRepository;
            _hubContext = hubContext;
            _animalRepository = animalRepository;
        }

        public async Task TriggerNoSignalAlertAsync(int animalId, int trackerId, string message)
        {
            var animal = await _animalRepository.GetByIdAsync(animalId);
            if (animal == null)
            {
                Console.WriteLine($"Warning: Animal with ID {animalId} not found for no signal alert.");
                return;
            }
            await CreateAlertAsync(animal, "NoSignal", "High", message);
        }

        public async Task ResolveNoSignalAlertAsync(int animalId, int trackerId, string message)
        {
            await ResolveActiveAlertsByTypeAsync(animalId, "NoSignal");
        }

        public async Task CheckLocationAlertsAsync(Animal animal, LocationHistory location)
        {
            // Check if animal is outside farm boundaries using new boundary system
            var farm = await _farmRepository.GetByIdWithBoundariesAsync(animal.FarmId);

            if (farm?.BoundaryCoordinates != null && farm.BoundaryCoordinates.Any())
            {
                bool isInsideFarm = GeofencingHelper.IsPointInPolygon(
                    location.Latitude,
                    location.Longitude,
                    farm.BoundaryCoordinates);

                if (!isInsideFarm)
                {
                    // Animal is outside - create alert if not already exists
                    await CreateAlertAsync(animal, "OutOfBounds", "High",
                        $"{animal.Name} has left the farm boundaries");
                }
                else
                {
                    // Animal is inside - auto-resolve any active OutOfBounds alerts
                    await ResolveActiveAlertsByTypeAsync(animal.Id, "OutOfBounds");
                }
            }

            // Check immobility using coordinate-based calculations
            var recentLocations = await _locationHistoryRepository
                .GetRecentLocationsAsync(animal.Id, 2);

            if (recentLocations.Count() >= 20)
            {
                var locations = recentLocations.ToList();
                var maxDistance = 0.0;
                var firstLocation = locations.First();

                foreach (var loc in locations)
                {
                    var distance = GeofencingHelper.CalculateDistance(
                        firstLocation.Latitude, firstLocation.Longitude,
                        loc.Latitude, loc.Longitude);

                    if (distance > maxDistance)
                        maxDistance = distance;
                }

                // If the animal hasn't moved more than 10 meters in the last 20 readings
                if (maxDistance < 10)
                {
                    await CreateAlertAsync(animal, "Immobility", "Medium",
                        $"{animal.Name} has been immobile for over 2 hours");
                }
            }
        }

        public async Task CheckActivityAlertsAsync(Animal animal, int activityLevel)
        {
            var avgActivity = await _locationHistoryRepository
                .GetAverageActivityLevelAsync(animal.Id, 24);

            if (avgActivity > 0)
            {
                if (activityLevel < avgActivity * 0.3 && activityLevel < 20)
                {
                    await CreateAlertAsync(animal, "LowActivity", "Medium",
                        $"{animal.Name} showing unusually low activity levels");
                }

                if (activityLevel > avgActivity * 2 && activityLevel > 80)
                {
                    await CreateAlertAsync(animal, "HighActivity", "Medium",
                        $"{animal.Name} showing unusually high activity levels");
                }
            }
        }

        public async Task CheckBreedingAlertsAsync(Animal animal)
        {
            var recentActivity = await _locationHistoryRepository
                .GetRecentLocationsAsync(animal.Id, 72); // 3 days

            var highActivityDays = recentActivity
                .Where(lh => lh.ActivityLevel > 70)
                .GroupBy(lh => lh.Timestamp.Date)
                .Where(g => g.Count() > 5)
                .Count();

            if (highActivityDays >= 2)
            {
                await CreateAlertAsync(animal, "PossibleHeat", "Low",
                    $"{animal.Name} may be in heat - elevated activity detected");
            }
        }

        /// <summary>
        /// Alertas de seguridad por posible robo
        /// </summary>
        public async Task CheckSecurityAlertsAsync(Animal animal, LocationHistory location)
        {
            await CheckNightMovementAlert(animal, location);
            await CheckSuddenExitAlert(animal, location);
            await CheckTrackerManipulationAlert(animal, location);
            await CheckUnusualSpeedAlert(animal, location);
        }

        /// <summary>
        /// Alertas de estado del tracker (batería, conectividad, hardware)
        /// </summary>
        public async Task CheckTrackerHealthAlertsAsync(Animal animal, LocationHistory location, Tracker tracker)
        {
            await CheckBatteryAlerts(animal, tracker);
            await CheckConnectivityAlerts(animal, tracker);
            await CheckLocationAccuracyAlerts(animal, location);
            await CheckTrackerDisconnectionAlert(animal, tracker);
        }

        private async Task CheckNightMovementAlert(Animal animal, LocationHistory location)
        {
            var currentHour = DateTime.Now.Hour;
            var isNightTime = currentHour >= 22 || currentHour <= 5; // 10 PM - 5 AM

            if (isNightTime)
            {
                var recentLocations = await _locationHistoryRepository
                    .GetRecentLocationsAsync(animal.Id, 3); // Últimas 3 lecturas

                if (recentLocations.Count() >= 3)
                {
                    var locations = recentLocations.ToList();
                    var totalDistance = 0.0;

                    for (int i = 1; i < locations.Count; i++)
                    {
                        var distance = GeofencingHelper.CalculateDistance(
                            locations[i-1].Latitude, locations[i-1].Longitude,
                            locations[i].Latitude, locations[i].Longitude);
                        totalDistance += distance;
                    }

                    // Si se movió más de 50 metros en horario nocturno
                    if (totalDistance > 50)
                    {
                        await CreateAlertAsync(animal, "NightMovement", "High",
                            $"{animal.Name} presenta movimiento anómalo durante la noche ({totalDistance:F0}m)");
                    }
                }
            }
        }

        private async Task CheckSuddenExitAlert(Animal animal, LocationHistory location)
        {
            var farm = await _farmRepository.GetByIdWithBoundariesAsync(animal.FarmId);

            if (farm?.BoundaryCoordinates != null && farm.BoundaryCoordinates.Any())
            {
                bool isInsideFarm = GeofencingHelper.IsPointInPolygon(
                    location.Latitude, location.Longitude, farm.BoundaryCoordinates);

                if (!isInsideFarm)
                {
                    // Verificar velocidad de salida
                    var recentLocations = await _locationHistoryRepository
                        .GetRecentLocationsAsync(animal.Id, 2);

                    if (recentLocations.Count() >= 2)
                    {
                        var locations = recentLocations.ToList();
                        var timeDiff = (locations[0].Timestamp - locations[1].Timestamp).TotalMinutes;

                        if (timeDiff > 0)
                        {
                            var distance = GeofencingHelper.CalculateDistance(
                                locations[1].Latitude, locations[1].Longitude,
                                locations[0].Latitude, locations[0].Longitude);

                            var speed = distance / (timeDiff / 60.0); // km/h

                            // Si salió a más de 15 km/h (velocidad anormal para ganado)
                            if (speed > 15)
                            {
                                await CreateAlertAsync(animal, "SuddenExit", "Critical",
                                    $"{animal.Name} salió del área a velocidad anormal ({speed:F1} km/h) - POSIBLE ROBO");
                            }
                        }
                    }
                }
            }
        }

        private async Task CheckUnusualSpeedAlert(Animal animal, LocationHistory location)
        {
            var recentLocations = await _locationHistoryRepository
                .GetRecentLocationsAsync(animal.Id, 3);

            if (recentLocations.Count() >= 3)
            {
                var locations = recentLocations.ToList();

                for (int i = 1; i < locations.Count; i++)
                {
                    var timeDiff = (locations[i-1].Timestamp - locations[i].Timestamp).TotalMinutes;

                    if (timeDiff > 0)
                    {
                        var distance = GeofencingHelper.CalculateDistance(
                            locations[i].Latitude, locations[i].Longitude,
                            locations[i-1].Latitude, locations[i-1].Longitude);

                        var speed = distance / (timeDiff / 60.0); // km/h

                        // Velocidad sostenida anormal para ganado (más de 20 km/h)
                        if (speed > 20)
                        {
                            await CreateAlertAsync(animal, "UnusualSpeed", "High",
                                $"{animal.Name} se desplaza a velocidad anormal ({speed:F1} km/h)");
                        }
                    }
                }
            }
        }

        private async Task CheckTrackerManipulationAlert(Animal animal, LocationHistory location)
        {
            // Verificar cambios bruscos en actividad que podrían indicar manipulación
            var recentLocations = await _locationHistoryRepository
                .GetRecentLocationsAsync(animal.Id, 5);

            if (recentLocations.Count() >= 5)
            {
                var activities = recentLocations.Select(l => l.ActivityLevel).ToList();
                var avgActivity = activities.Average();
                var currentActivity = location.ActivityLevel;

                // Cambio brusco en actividad (puede indicar manipulación del tracker)
                if (Math.Abs(currentActivity - avgActivity) > 50 && currentActivity > 90)
                {
                    await CreateAlertAsync(animal, "TrackerManipulation", "High",
                        $"{animal.Name} - Posible manipulación del tracker detectada");
                }
            }
        }

        private async Task CheckBatteryAlerts(Animal animal, Tracker tracker)
        {
            if (tracker.BatteryLevel <= 5)
            {
                await CreateAlertAsync(animal, "BatteryCritical", "Critical",
                    $"Tracker de {animal.Name} con batería crítica ({tracker.BatteryLevel}%)");
            }
            else if (tracker.BatteryLevel <= 20)
            {
                await CreateAlertAsync(animal, "BatteryLow", "High",
                    $"Tracker de {animal.Name} con batería baja ({tracker.BatteryLevel}%)");
            }
        }

        private async Task CheckConnectivityAlerts(Animal animal, Tracker tracker)
        {
            var timeSinceLastSeen = DateTime.UtcNow - tracker.LastSeen;

            if (timeSinceLastSeen.TotalMinutes > 30)
            {
                await CreateAlertAsync(animal, "NoSignal", "High",
                    $"Tracker de {animal.Name} sin señal por {timeSinceLastSeen.TotalMinutes:F0} minutos");
            }
            else if (timeSinceLastSeen.TotalMinutes > 10)
            {
                await CreateAlertAsync(animal, "WeakSignal", "Medium",
                    $"Tracker de {animal.Name} con señal débil por {timeSinceLastSeen.TotalMinutes:F0} minutos");
            }
        }

        private async Task CheckLocationAccuracyAlerts(Animal animal, LocationHistory location)
        {
            // Verificar coordenadas imposibles o erróneas
            if (Math.Abs(location.Latitude) > 90 || Math.Abs(location.Longitude) > 180)
            {
                await CreateAlertAsync(animal, "InvalidCoordinates", "High",
                    $"Tracker de {animal.Name} reportando coordenadas inválidas");
                return;
            }

            // Verificar saltos de ubicación imposibles
            var lastLocation = await _locationHistoryRepository
                .GetRecentLocationsAsync(animal.Id, 1);

            if (lastLocation.Any())
            {
                var previous = lastLocation.First();
                var distance = GeofencingHelper.CalculateDistance(
                    previous.Latitude, previous.Longitude,
                    location.Latitude, location.Longitude);

                var timeDiff = (location.Timestamp - previous.Timestamp).TotalMinutes;

                if (timeDiff > 0)
                {
                    var speed = distance / (timeDiff / 60.0); // km/h

                    // Salto de ubicación imposible (más de 100 km/h)
                    if (speed > 100)
                    {
                        await CreateAlertAsync(animal, "LocationJump", "Medium",
                            $"Tracker de {animal.Name} reporta salto de ubicación anormal ({distance:F0}m en {timeDiff:F0}min)");
                    }
                }
            }
        }

        private async Task CheckTrackerDisconnectionAlert(Animal animal, Tracker tracker)
        {
            // Verificar desconexión abrupta (tracker que estaba funcionando y se desconecta repentinamente)
            if (tracker.Status != "Active" && tracker.IsActive)
            {
                var recentLocations = await _locationHistoryRepository
                    .GetRecentLocationsAsync(animal.Id, 5);

                // Si tenía actividad reciente pero ahora está desconectado
                if (recentLocations.Any() && recentLocations.First().Timestamp > DateTime.UtcNow.AddMinutes(-10))
                {
                    await CreateAlertAsync(animal, "AbruptDisconnection", "High",
                        $"Tracker de {animal.Name} se desconectó abruptamente - posible manipulación");
                }
            }
        }

        public async Task<IEnumerable<AlertDto>> GetActiveAlertsAsync(int farmId)
        {
            var alerts = await _alertRepository.GetFarmAlertsAsync(farmId, true);

            return alerts.Select(a => new AlertDto
            {
                Id = a.Id,
                Type = a.Type,
                Title = GetAlertTitle(a.Type, a.Severity),
                Severity = a.Severity,
                Message = a.Message,
                AnimalId = a.AnimalId,
                FarmId = a.Animal?.FarmId,
                AnimalName = a.Animal?.Name ?? "N/A",
                FarmName = a.Animal?.Farm?.Name,
                IsRead = a.IsRead,
                IsResolved = a.IsResolved,
                CreatedAt = a.CreatedAt,
                ResolvedAt = a.ResolvedAt
            });
        }

        public async Task MarkAlertAsReadAsync(int alertId)
        {
            var alert = await _alertRepository.GetByIdAsync(alertId);
            if (alert != null)
            {
                alert.IsRead = true;
                await _alertRepository.UpdateAsync(alert);
            }
        }

        public async Task ResolveAlertAsync(int alertId)
        {
            var alert = await _alertRepository.GetByIdAsync(alertId);
            if (alert != null)
            {
                alert.IsResolved = true;
                alert.ResolvedAt = DateTime.UtcNow;
                await _alertRepository.UpdateAsync(alert);
            }
        }

        public async Task<IEnumerable<Alert>> GetActiveGeofencingAlertsAsync()
        {
            return await _alertRepository.GetActiveAlertsByTypeAsync("OutOfBounds");
        }

        public async Task ResolveAlertAsync(int alertId, string reason)
        {
            var alert = await _alertRepository.GetByIdAsync(alertId);
            if (alert != null)
            {
                alert.IsResolved = true;
                alert.ResolvedAt = DateTime.UtcNow;
                // Note: ResolvedBy field will be added in future migration
                await _alertRepository.UpdateAsync(alert);

                // Notify clients that the alert was resolved
                var alertWithAnimal = await _alertRepository.GetByIdAsync(alertId);
                if (alertWithAnimal?.Animal != null)
                {
                    var alertDto = new AlertDto
                    {
                        Id = alert.Id,
                        Type = alert.Type,
                        Title = GetAlertTitle(alert.Type, alert.Severity),
                        Severity = alert.Severity,
                        Message = alert.Message,
                        AnimalId = alert.AnimalId,
                        FarmId = alertWithAnimal.Animal.FarmId,
                        AnimalName = alertWithAnimal.Animal.Name,
                        FarmName = alertWithAnimal.Animal.Farm?.Name,
                        IsRead = alert.IsRead,
                        IsResolved = alert.IsResolved,
                        CreatedAt = alert.CreatedAt,
                        ResolvedAt = alert.ResolvedAt
                    };

                    await _hubContext.Clients.Group($"farm_{alertWithAnimal.Animal.FarmId}")
                        .SendAsync("AlertResolved", alertDto);
                }
            }
        }

        private async Task CreateAlertAsync(Animal animal, string type, string severity, string message)
        {
            var hasSimilar = await _alertRepository.HasSimilarAlertAsync(animal.Id, type, 24);
            if (hasSimilar) return;

            var alert = new Alert
            {
                Type = type,
                Severity = severity,
                Message = message,
                AnimalId = animal.Id,
                CreatedAt = DateTime.UtcNow
            };

            await _alertRepository.AddAsync(alert);

            var alertDto = new AlertDto
            {
                Id = alert.Id,
                Type = alert.Type,
                Title = GetAlertTitle(alert.Type, alert.Severity),
                Severity = alert.Severity,
                Message = alert.Message,
                AnimalId = alert.AnimalId,
                FarmId = animal.FarmId,
                AnimalName = animal.Name,
                IsRead = alert.IsRead,
                IsResolved = alert.IsResolved,
                CreatedAt = alert.CreatedAt,
                ResolvedAt = alert.ResolvedAt
            };

            await _hubContext.Clients.Group($"farm_{animal.FarmId}")
                .SendAsync("NewAlert", alertDto);
        }

        /// <summary>
        /// Resolves all active alerts of a specific type for an animal
        /// </summary>
        private async Task ResolveActiveAlertsByTypeAsync(int animalId, string alertType)
        {
            try
            {
                var activeAlerts = await _alertRepository.GetActiveAlertsByAnimalAndTypeAsync(animalId, alertType);

                foreach (var alert in activeAlerts)
                {
                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.UtcNow;
                    await _alertRepository.UpdateAsync(alert);

                    Console.WriteLine($"✅ Auto-resolved {alertType} alert for animal {animalId}: {alert.Message}");

                    // Notify clients that the alert was resolved
                    var alertDto = new AlertDto
                    {
                        Id = alert.Id,
                        Type = alert.Type,
                        Title = GetAlertTitle(alert.Type, alert.Severity),
                        Severity = alert.Severity,
                        Message = alert.Message,
                        AnimalId = alert.AnimalId,
                        IsRead = alert.IsRead,
                        IsResolved = alert.IsResolved,
                        CreatedAt = alert.CreatedAt,
                        ResolvedAt = alert.ResolvedAt
                    };

                    // Get animal info for the notification
                    var alertWithAnimal = await _alertRepository.GetByIdAsync(alert.Id);
                    if (alertWithAnimal?.Animal != null)
                    {
                        alertDto.FarmId = alertWithAnimal.Animal.FarmId;
                        alertDto.AnimalName = alertWithAnimal.Animal.Name;
                        alertDto.FarmName = alertWithAnimal.Animal.Farm?.Name;

                        await _hubContext.Clients.Group($"farm_{alertWithAnimal.Animal.FarmId}")
                            .SendAsync("AlertResolved", alertDto);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error resolving {alertType} alerts for animal {animalId}: {ex.Message}");
            }
        }

        private static string GetAlertTitle(string type, string severity)
        {
            return type switch
            {
                // Alertas existentes
                "OutOfBounds" => "🚨 Animal Fuera del Área",
                "LowActivity" => "😴 Baja Actividad",
                "HighActivity" => "🏃 Alta Actividad",
                "Immobility" => "🛑 Animal Inmóvil",
                "PossibleHeat" => "🔥 Posible Celo",

                // Alertas de seguridad
                "NightMovement" => "🌙 Movimiento Nocturno",
                "SuddenExit" => "🚨 Salida Súbita",
                "UnusualSpeed" => "🏎️ Velocidad Anormal",
                "TrackerManipulation" => "🔧 Manipulación de Tracker",
                "AbruptDisconnection" => "📡 Desconexión Abrupta",

                // Alertas de batería
                "BatteryLow" => "🔋 Batería Baja",
                "BatteryCritical" => "🪫 Batería Crítica",

                // Alertas de conectividad
                "NoSignal" => "📶 Sin Señal",
                "WeakSignal" => "📊 Señal Débil",

                // Alertas de ubicación/hardware
                "InvalidCoordinates" => "📍 Coordenadas Inválidas",
                "LocationJump" => "🚀 Salto de Ubicación",

                _ => $"⚠️ Alerta {severity}"
            };
        }
    }
}

