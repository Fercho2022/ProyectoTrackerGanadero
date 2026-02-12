using ApiWebTrackerGanado.Models;
using NetTopologySuite.Geometries;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface ILocationHistoryRepository
    {
        Task<LocationHistory?> GetByIdAsync(int id);
        Task<IEnumerable<LocationHistory>> GetAllAsync();
        Task<LocationHistory> AddAsync(LocationHistory locationHistory);
        Task UpdateAsync(LocationHistory locationHistory);
        Task DeleteAsync(LocationHistory locationHistory);
        Task<IEnumerable<LocationHistory>> GetAnimalLocationHistoryAsync(int animalId, DateTime from, DateTime to);
        Task<LocationHistory?> GetAnimalLastLocationAsync(int animalId);
        Task<IEnumerable<LocationHistory>> GetRecentLocationsAsync(int animalId, int hours = 2);
        Task<IEnumerable<LocationHistory>> GetLocationsInAreaAsync(Polygon area, DateTime from, DateTime to);
        Task<double> GetAverageActivityLevelAsync(int animalId, int hours = 24);
        Task<IEnumerable<LocationHistory>> GetLocationsByTrackerAsync(int trackerId, DateTime from, DateTime to);
        Task<LocationHistory?> GetLatestLocationAsync(int animalId, TimeSpan withinTimeSpan);
    }
}
