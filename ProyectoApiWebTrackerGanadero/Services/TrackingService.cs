using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.AspNetCore.SignalR;
using NetTopologySuite.Geometries;
using NetTopologySuite;
using Microsoft.EntityFrameworkCore;
using ApiWebTrackerGanado.Hubs;
using ApiWebTrackerGanado.Helpers;

namespace ApiWebTrackerGanado.Services
{
    public class TrackingService : ITrackingService
    {
        private readonly ITrackerRepository _trackerRepository;
        private readonly ILocationHistoryRepository _locationHistoryRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IHubContext<LiveTrackingHub> _hubContext;
        private readonly IAlertService _alertService;
        private readonly CattleTrackingContext _context;

        // Throttle: only check alerts every 60 seconds per tracker to reduce DB load
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _lastAlertCheck = new();
        private static readonly TimeSpan AlertCheckInterval = TimeSpan.FromSeconds(60);

        public TrackingService(
            ITrackerRepository trackerRepository,
            ILocationHistoryRepository locationHistoryRepository,
            IAnimalRepository animalRepository,
            IHubContext<LiveTrackingHub> hubContext,
            IAlertService alertService,
            CattleTrackingContext context)
        {
            _trackerRepository = trackerRepository;
            _locationHistoryRepository = locationHistoryRepository;
            _animalRepository = animalRepository;
            _hubContext = hubContext;
            _alertService = alertService;
            _context = context;
        }

        public async Task ProcessTrackerDataAsync(TrackerDataDto trackerData)
        {
            // 1. Find or Create Tracker - single query with needed includes
            var tracker = await _context.Trackers
                .Include(t => t.CustomerTrackers.Where(ct => ct.Status == "Active"))
                .FirstOrDefaultAsync(t => t.DeviceId == trackerData.DeviceId);

            if (tracker == null)
            {
                // Tracker nuevo - crear como "Discovered"
                tracker = new Tracker
                {
                    DeviceId = trackerData.DeviceId,
                    Name = $"Tracker {trackerData.DeviceId}",
                    Model = "Unknown",
                    Status = "Discovered",
                    IsAvailableForAssignment = true,
                    IsOnline = true,
                    BatteryLevel = trackerData.BatteryLevel,
                    LastSeen = trackerData.Timestamp.ToUniversalTime(),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Trackers.Add(tracker);
                await _context.SaveChangesAsync();
                return;
            }

            // Update tracker state (tracked by EF, saved later in single batch)
            tracker.BatteryLevel = trackerData.BatteryLevel;
            tracker.LastSeen = trackerData.Timestamp.ToUniversalTime();
            tracker.IsOnline = true;

            if (tracker.Status == "Discovered")
            {
                await _context.SaveChangesAsync();
                return;
            }

            // 2. Find assigned animal in a single, more reliable query
            var assignedAnimal = await _context.CustomerTrackers
                .Where(ct => ct.Tracker.DeviceId == trackerData.DeviceId && ct.Status == "Active")
                .Select(ct => ct.AssignedAnimal)
                .Include(animal => animal.Farm)
                .FirstOrDefaultAsync();


            // 3. Create LocationHistory entry (add to context without saving yet)
            var locationHistory = new LocationHistory
            {
                TrackerId = tracker.Id,
                DeviceId = trackerData.DeviceId,
                Latitude = trackerData.Latitude,
                Longitude = trackerData.Longitude,
                Altitude = trackerData.Altitude,
                Speed = trackerData.Speed,
                ActivityLevel = trackerData.ActivityLevel,
                Temperature = trackerData.Temperature,
                SignalStrength = trackerData.SignalStrength,
                Timestamp = trackerData.Timestamp.ToUniversalTime(),
                AnimalId = assignedAnimal?.Id
            };

            _context.LocationHistories.Add(locationHistory);

            // 4. SINGLE SaveChangesAsync for tracker update + location history insert
            await _context.SaveChangesAsync();

            // 5. Alerts: throttle to once per 60 seconds per tracker to reduce DB load
            if (assignedAnimal != null)
            {
                var now = DateTime.UtcNow;
                var shouldCheckAlerts = _lastAlertCheck.AddOrUpdate(
                    trackerData.DeviceId,
                    now, // first time: always check
                    (key, lastCheck) => (now - lastCheck) >= AlertCheckInterval ? now : lastCheck
                ) == now;

                if (shouldCheckAlerts)
                {
                    await _alertService.CheckLocationAlertsAsync(assignedAnimal, locationHistory);
                    await _alertService.CheckActivityAlertsAsync(assignedAnimal, trackerData.ActivityLevel);
                    await _alertService.CheckSecurityAlertsAsync(assignedAnimal, locationHistory);
                    await _alertService.CheckTrackerHealthAlertsAsync(assignedAnimal, locationHistory, tracker);
                }

                // SignalR broadcast (always, these are cheap)
                var locationDto = new LocationDto
                {
                    Latitude = trackerData.Latitude,
                    Longitude = trackerData.Longitude,
                    Altitude = trackerData.Altitude,
                    Speed = trackerData.Speed,
                    ActivityLevel = trackerData.ActivityLevel,
                    Temperature = trackerData.Temperature,
                    Timestamp = trackerData.Timestamp,
                    HasSignal = true
                };

                await _hubContext.Clients.Group($"animal_{assignedAnimal.Id}")
                    .SendAsync("LocationUpdate", assignedAnimal.Id, locationDto);

                await _hubContext.Clients.Group($"farm_{assignedAnimal.FarmId}")
                    .SendAsync("AnimalLocationUpdate", assignedAnimal.Id, locationDto);
            }
        }

        public async Task<IEnumerable<LocationDto>> GetAnimalLocationHistoryAsync(int animalId, DateTime from, DateTime to)
        {
            var locations = await _locationHistoryRepository
                .GetAnimalLocationHistoryAsync(animalId, from, to);

            return locations.Select(lh => new LocationDto
            {
                Latitude = lh.Latitude,
                Longitude = lh.Longitude,
                Altitude = lh.Altitude,
                Speed = lh.Speed,
                ActivityLevel = lh.ActivityLevel,
                Temperature = lh.Temperature,
                Timestamp = lh.Timestamp
            });
        }

        public async Task<LocationDto?> GetAnimalCurrentLocationAsync(int animalId)
        {
            var animalWithTracker = await _animalRepository.GetAnimalWithTrackerAsync(animalId);

            if (animalWithTracker == null || animalWithTracker.Tracker == null)
            {
                return new LocationDto
                {
                    Latitude = 0.0,
                    Longitude = 0.0,
                    Altitude = 0.0,
                    Speed = 0.0,
                    ActivityLevel = 0,
                    Temperature = 0.0,
                    Timestamp = DateTime.UtcNow,
                    HasSignal = false
                };
            }

            // Use the IsOnline status maintained by the background service
            if (!animalWithTracker.Tracker.IsOnline)
            {
                return new LocationDto
                {
                    Latitude = 0.0,
                    Longitude = 0.0,
                    Altitude = 0.0,
                    Speed = 0.0,
                    ActivityLevel = 0,
                    Temperature = 0.0,
                    Timestamp = animalWithTracker.Tracker.LastSeen, // Use LastSeen as timestamp for no-signal state
                    HasSignal = false
                };
            }

            // If tracker is online, get the last known location
            var lastLocation = await _locationHistoryRepository.GetAnimalLastLocationAsync(animalId);

            if (lastLocation == null)
            {
                return new LocationDto
                {
                    Latitude = 0.0,
                    Longitude = 0.0,
                    Altitude = 0.0,
                    Speed = 0.0,
                    ActivityLevel = 0,
                    Temperature = 0.0,
                    Timestamp = animalWithTracker.Tracker.LastSeen, // Tracker is online but no location history yet
                    HasSignal = true
                };
            }

            return new LocationDto
            {
                Latitude = lastLocation.Latitude,
                Longitude = lastLocation.Longitude,
                Altitude = lastLocation.Altitude,
                Speed = lastLocation.Speed,
                ActivityLevel = lastLocation.ActivityLevel,
                Temperature = lastLocation.Temperature,
                Timestamp = lastLocation.Timestamp,
                HasSignal = true
            };
        }

        public async Task<IEnumerable<AnimalDto>> GetAnimalsInAreaAsync(double lat1, double lng1, double lat2, double lng2)
        {
            var area = GeometryHelper.CreateRectangle(lng1, lat1, lng2, lat2);
            var animalsInArea = await _locationHistoryRepository.GetLocationsInAreaAsync(area, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

            return animalsInArea
                .GroupBy(lh => lh.AnimalId)
                .Select(g => new AnimalDto
                {
                    Id = g.First().Animal.Id,
                    Name = g.First().Animal.Name,
                    Tag = g.First().Animal.Tag,
                    BirthDate = g.First().Animal.BirthDate,
                    Gender = g.First().Animal.Gender,
                    Breed = g.First().Animal.Breed,
                    Weight = g.First().Animal.Weight,
                    Status = g.First().Animal.Status,
                    FarmId = g.First().Animal.FarmId,
                    TrackerId = g.First().Animal.TrackerId
                });
        }

        public async Task<IEnumerable<object>> GetFarmAnimalsLocationsAsync(int farmId, int customerId)
        {
            // First, verify that the farm belongs to the customer
            var farm = await _context.Farms
                .FirstOrDefaultAsync(f => f.Id == farmId && _context.Customers.Any(c => c.Id == customerId && c.UserId == f.UserId));

            if (farm == null)
            {
                throw new ArgumentException($"Farm with ID {farmId} not found or does not belong to customer {customerId}.");
            }

            // OPTIMIZADO: Cargar animales con trackers en una sola query
            var animals = await _context.Animals
                .Include(a => a.Tracker)
                .Where(a => a.FarmId == farmId)
                .ToListAsync();

            // OPTIMIZADO: Obtener última ubicación de TODOS los animales de la granja en UNA sola query
            var animalIds = animals.Select(a => a.Id).ToList();
            var lastLocations = await _context.LocationHistories
                .Where(lh => lh.AnimalId.HasValue && animalIds.Contains(lh.AnimalId.Value))
                .GroupBy(lh => lh.AnimalId)
                .Select(g => new
                {
                    AnimalId = g.Key,
                    Location = g.OrderByDescending(lh => lh.Timestamp).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.AnimalId!.Value, x => x.Location);

            var result = new List<object>();

            foreach (var animal in animals)
            {
                LocationDto? locationDto = null;
                var tracker = animal.Tracker;
                var isOnline = tracker?.IsOnline ?? false;

                // Siempre usar coordenadas reales de la ultima ubicacion conocida
                // para que animales desconectados queden visibles en el mapa (en gris)
                if (lastLocations.TryGetValue(animal.Id, out var lastLoc) && lastLoc != null)
                {
                    locationDto = new LocationDto
                    {
                        Latitude = lastLoc.Latitude,
                        Longitude = lastLoc.Longitude,
                        Altitude = lastLoc.Altitude,
                        Speed = isOnline ? lastLoc.Speed : 0.0,
                        ActivityLevel = isOnline ? lastLoc.ActivityLevel : 0,
                        Temperature = lastLoc.Temperature,
                        Timestamp = lastLoc.Timestamp,
                        HasSignal = isOnline
                    };
                }
                else if (tracker != null)
                {
                    locationDto = new LocationDto
                    {
                        Latitude = 0.0, Longitude = 0.0, Altitude = 0.0,
                        Speed = 0.0, ActivityLevel = 0, Temperature = 0.0,
                        Timestamp = tracker.LastSeen,
                        HasSignal = false
                    };
                }

                result.Add(new
                {
                    Id = animal.Id,
                    Name = animal.Name,
                    Tag = animal.Tag,
                    Status = animal.Status,
                    FarmId = animal.FarmId,
                    TrackerId = animal.TrackerId,
                    CurrentLocation = locationDto,
                    HasSignal = locationDto?.HasSignal ?? false
                });
            }

            return result;
        }

        public async Task<IEnumerable<object>> GetAllAnimalsLocationsAsync(int customerId)
        {
            // OPTIMIZADO: Cargar todos los animales del customer con tracker en una sola query
            var animals = await _context.Animals
                .Include(a => a.Tracker)
                .Where(a => a.CustomerTracker != null &&
                            a.CustomerTracker.CustomerId == customerId &&
                            a.CustomerTracker.Status == "Active")
                .ToListAsync();

            // OPTIMIZADO: Obtener última ubicación de TODOS los animales en UNA sola query
            var animalIds = animals.Select(a => a.Id).ToList();
            var lastLocations = await _context.LocationHistories
                .Where(lh => lh.AnimalId.HasValue && animalIds.Contains(lh.AnimalId.Value))
                .GroupBy(lh => lh.AnimalId)
                .Select(g => new
                {
                    AnimalId = g.Key,
                    Location = g.OrderByDescending(lh => lh.Timestamp).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.AnimalId!.Value, x => x.Location);

            var result = new List<object>();

            foreach (var animal in animals)
            {
                LocationDto? locationDto = null;
                var tracker = animal.Tracker;
                var isOnline = tracker?.IsOnline ?? false;

                // Siempre usar coordenadas reales de la ultima ubicacion conocida
                // para que animales desconectados queden visibles en el mapa (en gris)
                if (lastLocations.TryGetValue(animal.Id, out var lastLoc) && lastLoc != null)
                {
                    locationDto = new LocationDto
                    {
                        Latitude = lastLoc.Latitude,
                        Longitude = lastLoc.Longitude,
                        Altitude = lastLoc.Altitude,
                        Speed = isOnline ? lastLoc.Speed : 0.0,
                        ActivityLevel = isOnline ? lastLoc.ActivityLevel : 0,
                        Temperature = lastLoc.Temperature,
                        Timestamp = lastLoc.Timestamp,
                        HasSignal = isOnline
                    };
                }
                else if (tracker != null)
                {
                    locationDto = new LocationDto
                    {
                        Latitude = 0.0, Longitude = 0.0, Altitude = 0.0,
                        Speed = 0.0, ActivityLevel = 0, Temperature = 0.0,
                        Timestamp = tracker.LastSeen,
                        HasSignal = false
                    };
                }

                result.Add(new
                {
                    Id = animal.Id,
                    Name = animal.Name,
                    Tag = animal.Tag,
                    Status = animal.Status,
                    FarmId = animal.FarmId,
                    TrackerId = animal.TrackerId,
                    CurrentLocation = locationDto,
                    HasSignal = locationDto?.HasSignal ?? false
                });
            }

            return result;
        }

        public async Task SaveLocationHistoryAsync(SaveLocationHistoryDto locationData)
        {
            // Verify that the animal and tracker exist
            var animal = await _animalRepository.GetByIdAsync(locationData.AnimalId);
            if (animal == null)
                throw new ArgumentException($"Animal with ID {locationData.AnimalId} not found");

            var tracker = await _trackerRepository.GetByIdAsync(locationData.TrackerId);
            if (tracker == null)
                throw new ArgumentException($"Tracker with ID {locationData.TrackerId} not found");

            // Create location history entry from frontend data
            var locationHistory = new LocationHistory
            {
                AnimalId = locationData.AnimalId,
                TrackerId = locationData.TrackerId,
                Latitude = locationData.Latitude,
                Longitude = locationData.Longitude,
                Altitude = locationData.Altitude,
                Speed = locationData.Speed,
                ActivityLevel = locationData.ActivityLevel,
                Temperature = locationData.Temperature,
                SignalStrength = locationData.SignalStrength,
                Timestamp = locationData.Timestamp.Kind == DateTimeKind.Utc
                    ? locationData.Timestamp
                    : locationData.Timestamp.ToUniversalTime()
            };

            await _locationHistoryRepository.AddAsync(locationHistory);

            // Check for alerts based on the new location
            await _alertService.CheckLocationAlertsAsync(animal, locationHistory);
            await _alertService.CheckActivityAlertsAsync(animal, locationData.ActivityLevel);

            // Check security alerts
            await _alertService.CheckSecurityAlertsAsync(animal, locationHistory);

            // Check tracker health (need to get the tracker)
            if (tracker != null)
            {
                await _alertService.CheckTrackerHealthAlertsAsync(animal, locationHistory, tracker);
            }

            // Send real-time update via SignalR
            var locationDto = new LocationDto
            {
                Latitude = locationData.Latitude,
                Longitude = locationData.Longitude,
                Altitude = locationData.Altitude,
                Speed = locationData.Speed,
                ActivityLevel = locationData.ActivityLevel,
                Temperature = locationData.Temperature,
                Timestamp = locationData.Timestamp
            };

            await _hubContext.Clients.Group($"animal_{locationData.AnimalId}")
                .SendAsync("LocationUpdate", locationData.AnimalId, locationDto);

            await _hubContext.Clients.Group($"farm_{animal.FarmId}")
                .SendAsync("AnimalLocationUpdate", locationData.AnimalId, locationDto);
        }
    }
}
