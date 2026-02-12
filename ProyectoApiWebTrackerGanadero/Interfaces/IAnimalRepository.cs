using ApiWebTrackerGanado.Models;
using NetTopologySuite.Geometries;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IAnimalRepository
    {
        Task<Animal?> GetByIdAsync(int id);
        Task<IEnumerable<Animal>> GetAllAsync();
        Task<Animal> AddAsync(Animal animal);
        Task UpdateAsync(Animal animal);
        Task DeleteAsync(Animal animal);
        Task<IEnumerable<Animal>> GetAnimalsByFarmAsync(int farmId);
        Task<IEnumerable<Animal>> GetAnimalsWithTrackersAsync(int farmId);
        Task<Animal?> GetAnimalWithDetailsAsync(int id);
        Task<IEnumerable<Animal>> GetAnimalsInAreaAsync(Polygon area);
        Task<IEnumerable<Animal>> GetAnimalsOutsideBoundariesAsync(int farmId);
        Task<IEnumerable<Animal>> GetBreedingFemalesAsync(int farmId);
        Task<IEnumerable<Animal>> GetAnimalsByStatusAsync(int farmId, string status);
        Task<decimal> GetAverageWeightByBreedAsync(int farmId, string breed);
        Task<bool> ExistsAsync(int id);
        Task<int> CountAsync();
        Task<Animal?> GetByIdWithLocationAsync(int id);
        Task<Animal?> GetAnimalWithTrackerAsync(int animalId); // New method
    }
}
