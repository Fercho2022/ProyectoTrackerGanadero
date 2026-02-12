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
            // 1. Find or Create Tracker
            var tracker = await _context.Trackers
                .Include(t => t.CustomerTrackers.Where(ct => ct.Status == "Active")) // Include active CustomerTrackers
                .ThenInclude(ct => ct.Customer)
                .Include(t => t.Animal) // Include assigned Animal directly to the tracker
                .FirstOrDefaultAsync(t => t.DeviceId == trackerData.DeviceId);

            if (tracker == null)
            {
                // Tracker nuevo detectado - crearlo como "Discovered" esperando registro del usuario
                tracker = new Tracker
                {
                    DeviceId = trackerData.DeviceId,
                    Name = $"Tracker {trackerData.DeviceId}",
                    Model = "Unknown",
                    Status = "Discovered", // Esperando registro manual
                    IsAvailableForAssignment = true, // Disponible para que cualquier usuario lo registre
                    IsOnline = true,
                    BatteryLevel = trackerData.BatteryLevel,
                    LastSeen = trackerData.Timestamp.ToUniversalTime(),
                    CreatedAt = DateTime.UtcNow
                };

                // NO crear CustomerTracker aquí - el usuario lo registra desde la app
                // NO guardar LocationHistory para trackers "Discovered"

                _context.Trackers.Add(tracker);
                await _context.SaveChangesAsync();

                // Salir aquí - no procesar LocationHistory ni alertas para trackers sin registrar
                return;
            }
            else
            {
                // Tracker existe - actualizar estado
                tracker.BatteryLevel = trackerData.BatteryLevel;
                tracker.LastSeen = trackerData.Timestamp.ToUniversalTime();
                tracker.IsOnline = true;
                await _context.SaveChangesAsync();

                // Si el tracker está "Discovered" (sin registrar), no procesar más
                if (tracker.Status == "Discovered")
                {
                    return;
                }

                // Si el tracker está "Active", continuar con LocationHistory y alertas
                // El código siguiente solo se ejecuta para trackers registrados
            }

            var activeCustomerTracker = tracker.CustomerTrackers.FirstOrDefault(ct => ct.Status == "Active");
            Animal? assignedAnimal = null;

            if (activeCustomerTracker != null)
            {
                // If there's an active CustomerTracker, try to get the animal assigned to it
                // Note: Animal is directly linked to CustomerTracker (AssignedAnimal) AND Tracker (Animal navigation property)
                // We should prioritize the Animal linked via CustomerTracker if it exists, otherwise use the one directly linked to Tracker.
                assignedAnimal = await _context.Animals
                    .Include(a => a.Farm)
                    .FirstOrDefaultAsync(a => a.CustomerTrackerId == activeCustomerTracker.Id);
                
                if (assignedAnimal == null && tracker.Animal != null)
                {
                     // Fallback: If no animal linked via CustomerTracker, but tracker is directly linked to an animal
                    assignedAnimal = await _context.Animals
                        .Include(a => a.Farm)
                        .FirstOrDefaultAsync(a => a.Id == tracker.Animal.Id);
                }
            }
            else if (tracker.Animal != null)
            {
                // No active CustomerTracker, but tracker is directly linked to an animal
                assignedAnimal = await _context.Animals
                    .Include(a => a.Farm)
                    .FirstOrDefaultAsync(a => a.Id == tracker.Animal.Id);
            }


            // Create the entry in the location history
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
                Timestamp = trackerData.Timestamp.ToUniversalTime()
            };

            // Link to Animal if one is assigned
            if (assignedAnimal != null)
            {
                locationHistory.AnimalId = assignedAnimal.Id;
            }

            await _locationHistoryRepository.AddAsync(locationHistory);

            // Conditional logic for alerts and SignalR updates
            if (assignedAnimal != null)
            {
                await _alertService.CheckLocationAlertsAsync(assignedAnimal, locationHistory);
                await _alertService.CheckActivityAlertsAsync(assignedAnimal, trackerData.ActivityLevel);
                await _alertService.CheckSecurityAlertsAsync(assignedAnimal, locationHistory);
                await _alertService.CheckTrackerHealthAlertsAsync(assignedAnimal, locationHistory, tracker);

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
            else
            {
                // If no animal is assigned but tracker belongs to a customer, we could send a generic customer-level update
                if (activeCustomerTracker != null && activeCustomerTracker.Customer != null)
                {
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
                    // Example: send update to a customer-specific group
                    await _hubContext.Clients.Group($"customer_{activeCustomerTracker.Customer.Id}")
                        .SendAsync("UnassignedTrackerLocationUpdate", tracker.Id, locationDto);
                }
                // No animal and no customer assignment, just save history for discovery.
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

            // If farm is valid, get the animals and their locations
            var animals = await _context.Animals
                .Include(a => a.Tracker)
                .Where(a => a.FarmId == farmId)
                .ToListAsync();

            var result = new List<object>();

            foreach (var animal in animals)
            {
                var currentLocation = await GetAnimalCurrentLocationAsync(animal.Id);
                result.Add(new
                {
                    Id = animal.Id,
                    Name = animal.Name,
                    Tag = animal.Tag,
                    Status = animal.Status,
                    FarmId = animal.FarmId,
                    TrackerId = animal.TrackerId,
                    CurrentLocation = currentLocation,
                    HasSignal = currentLocation?.HasSignal ?? false
                });
            }

            return result;
        }

        public async Task<IEnumerable<object>> GetAllAnimalsLocationsAsync(int customerId)
        {
            // Get all animals associated with the customer via an active CustomerTracker
            var animals = await _context.Animals
                .Include(a => a.Tracker)
                .Include(a => a.CustomerTracker)
                    .ThenInclude(ct => ct.Customer) // Ensure Customer is loaded if needed later
                .Where(a => a.CustomerTracker != null &&
                            a.CustomerTracker.CustomerId == customerId &&
                            a.CustomerTracker.Status == "Active")
                .ToListAsync();

            var result = new List<object>();

            foreach (var animal in animals)
            {
                var currentLocation = await GetAnimalCurrentLocationAsync(animal.Id);

                result.Add(new
                {
                    Id = animal.Id,
                    Name = animal.Name,
                    Tag = animal.Tag,
                    Status = animal.Status,
                    FarmId = animal.FarmId,
                    TrackerId = animal.TrackerId,
                    CurrentLocation = currentLocation,
                    HasSignal = currentLocation?.HasSignal ?? false // Explicitly include HasSignal
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
