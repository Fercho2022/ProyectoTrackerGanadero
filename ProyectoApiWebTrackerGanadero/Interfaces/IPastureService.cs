using ApiWebTrackerGanado.Dtos;

namespace ApiWebTrackerGanado.Interfaces
{
    public interface IPastureService
    {

        Task<IEnumerable<PastureUsageDto>> GetPastureUsageReportAsync(int farmId, DateTime from, DateTime to);
        Task UpdatePastureUsageAsync();
        Task<IEnumerable<PastureUsageDto>> GetCurrentPastureOccupancyAsync(int farmId);
    }
}
