using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IAlertRepository
    {
        Task<Alert?> GetByIdAsync(int id);
        Task<IEnumerable<Alert>> GetAllAsync();
        Task<Alert> AddAsync(Alert alert);
        Task UpdateAsync(Alert alert);
        Task DeleteAsync(Alert alert);
        Task<IEnumerable<Alert>> GetFarmAlertsAsync(int farmId, bool onlyActive = true);
        Task<IEnumerable<Alert>> GetAnimalAlertsAsync(int animalId, bool onlyActive = true);
        Task<IEnumerable<Alert>> GetAlertsByTypeAsync(int farmId, string type);
        Task<IEnumerable<Alert>> GetCriticalAlertsAsync(int farmId);
        Task<int> GetUnreadAlertsCountAsync(int farmId);
        Task<bool> HasSimilarAlertAsync(int animalId, string type, int hoursBack = 24);
        Task<IEnumerable<Alert>> GetActiveAlertsByAnimalAndTypeAsync(int animalId, string type);
        Task<IEnumerable<Alert>> GetActiveAlertsByTypeAsync(string type);
    }
}
