using ApiWebTrackerGanado.Dtos;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface ITrackingService
    {
        Task ProcessTrackerDataAsync(TrackerDataDto trackerData);
        Task<IEnumerable<LocationDto>> GetAnimalLocationHistoryAsync(int animalId, DateTime from, DateTime to);
        Task<LocationDto?> GetAnimalCurrentLocationAsync(int animalId);
        Task<IEnumerable<AnimalDto>> GetAnimalsInAreaAsync(double lat1, double lng1, double lat2, double lng2);
        Task<IEnumerable<object>> GetFarmAnimalsLocationsAsync(int farmId, int customerId);
        Task<IEnumerable<object>> GetAllAnimalsLocationsAsync(int customerId);
        Task SaveLocationHistoryAsync(SaveLocationHistoryDto locationData);
    }
}

