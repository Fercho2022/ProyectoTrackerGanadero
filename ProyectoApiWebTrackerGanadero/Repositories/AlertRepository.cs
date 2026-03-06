using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Repositories
{
    public class AlertRepository : IAlertRepository
    {

        private readonly CattleTrackingContext _context;

        public AlertRepository(CattleTrackingContext context)
        {
            _context = context;
        }

        public async Task<Alert?> GetByIdAsync(int id)
        {
            return await _context.Alerts.FindAsync(id);
        }

        public async Task<IEnumerable<Alert>> GetAllAsync()
        {
            return await _context.Alerts
                .AsNoTracking()
                .Include(a => a.Animal)
                .ThenInclude(a => a.Farm)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<Alert> AddAsync(Alert alert)
        {
            _context.Alerts.Add(alert);
            await _context.SaveChangesAsync();
            return alert;
        }

        public async Task UpdateAsync(Alert alert)
        {
            _context.Alerts.Update(alert);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Alert alert)
        {
            _context.Alerts.Remove(alert);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Alert>> GetFarmAlertsAsync(int farmId, bool onlyActive = true)
        {
            var query = _context.Alerts
                .AsNoTracking()
                .Include(a => a.Animal)
                .ThenInclude(a => a.Farm)
                .Where(a => a.Animal.FarmId == farmId);

            if (onlyActive)
                query = query.Where(a => !a.IsResolved);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Alert>> GetAnimalAlertsAsync(int animalId, bool onlyActive = true)
        {
            var query = _context.Alerts.AsNoTracking().Where(a => a.AnimalId == animalId);

            if (onlyActive)
                query = query.Where(a => !a.IsResolved);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Alert>> GetAlertsByTypeAsync(int farmId, string type)
        {
            return await _context.Alerts
                .AsNoTracking()
                .Include(a => a.Animal)
                .Where(a => a.Animal.FarmId == farmId && a.Type == type)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Alert>> GetCriticalAlertsAsync(int farmId)
        {
            return await _context.Alerts
                .AsNoTracking()
                .Include(a => a.Animal)
                .Where(a => a.Animal.FarmId == farmId &&
                           a.Severity == "Critical" &&
                           !a.IsResolved)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadAlertsCountAsync(int farmId)
        {
            return await _context.Alerts
                .AsNoTracking()
                .CountAsync(a => a.Animal.FarmId == farmId && !a.IsRead && !a.IsResolved);
        }

        public async Task<bool> HasSimilarAlertAsync(int animalId, string type, int hoursBack = 24)
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-hoursBack);
            return await _context.Alerts
                .AnyAsync(a => a.AnimalId == animalId &&
                              a.Type == type &&
                              !a.IsResolved &&
                              a.CreatedAt >= cutoffTime);
        }

        public async Task<IEnumerable<Alert>> GetActiveAlertsByAnimalAndTypeAsync(int animalId, string type)
        {
            return await _context.Alerts
                .AsNoTracking()
                .Where(a => a.AnimalId == animalId &&
                           a.Type == type &&
                           !a.IsResolved)
                .ToListAsync();
        }

        public async Task<IEnumerable<Alert>> GetActiveAlertsByTypeAsync(string type)
        {
            return await _context.Alerts
                .AsNoTracking()
                .Include(a => a.Animal)
                    .ThenInclude(a => a.Farm)
                .Where(a => a.Type == type && !a.IsResolved)
                .ToListAsync();
        }
    }
}
