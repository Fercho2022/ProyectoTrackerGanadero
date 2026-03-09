using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Models;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IAlertService
    {
        Task CheckLocationAlertsAsync(Animal animal, LocationHistory location);
        Task CheckActivityAlertsAsync(Animal animal, int activityLevel);
        Task CheckBreedingAlertsAsync(Animal animal);
        Task CheckSecurityAlertsAsync(Animal animal, LocationHistory location);
        Task CheckTrackerHealthAlertsAsync(Animal animal, LocationHistory location, Tracker tracker);
        Task<IEnumerable<AlertDto>> GetActiveAlertsAsync(int farmId);
        Task MarkAlertAsReadAsync(int alertId);
        Task ResolveAlertAsync(int alertId);
        Task<IEnumerable<Alert>> GetActiveGeofencingAlertsAsync();
        Task ResolveAlertAsync(int alertId, string reason);
        Task TriggerNoSignalAlertAsync(int animalId, int trackerId, string message);
        Task ResolveNoSignalAlertAsync(int animalId, int trackerId, string message);
        Task CreateMassDisconnectionAlertAsync(int animalId, string message);
    }
}
