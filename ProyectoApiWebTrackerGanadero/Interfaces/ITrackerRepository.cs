using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface ITrackerRepository
    {
        Task<Tracker?> GetByIdAsync(int id);
        Task<IEnumerable<Tracker>> GetAllAsync();
        Task<Tracker> AddAsync(Tracker tracker);
        Task UpdateAsync(Tracker tracker);
        Task DeleteAsync(Tracker tracker);
        Task<Tracker?> GetByDeviceIdAsync(string deviceId);
        Task<IEnumerable<Tracker>> GetActiveTrackersAsync();
        Task<IEnumerable<CustomerTracker>> GetTrackersByUserIdAsync(int userId);
        Task<IEnumerable<Tracker>> GetTrackersWithLowBatteryAsync(int threshold = 20);
        Task<Tracker?> GetTrackerWithAnimalAsync(string deviceId);
        Task<bool> ExistsAsync(string deviceId);
        Task<IEnumerable<Tracker>> GetAllTrackersWithAnimalsAsync();
    }
}
