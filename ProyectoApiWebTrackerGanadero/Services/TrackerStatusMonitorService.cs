using ApiWebTrackerGanado.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApiWebTrackerGanado.Services
{
    /// <summary>
    /// Servicio en background que monitorea el estado de los trackers
    /// y marca como offline aquellos que no han enviado datos recientemente
    /// </summary>
    public class TrackerStatusMonitorService : BackgroundService
    {
        private readonly ILogger<TrackerStatusMonitorService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Verificar cada minuto
        private readonly TimeSpan _offlineThreshold = TimeSpan.FromMinutes(5); // Considerar offline después de 5 minutos sin datos

        public TrackerStatusMonitorService(
            ILogger<TrackerStatusMonitorService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 TrackerStatusMonitorService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UpdateTrackerStatuses();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error actualizando estados de trackers");
                }

                // Esperar antes del próximo ciclo
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("🛑 TrackerStatusMonitorService detenido");
        }

        private async Task UpdateTrackerStatuses()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CattleTrackingContext>();

            var now = DateTime.UtcNow;
            var offlineThreshold = now - _offlineThreshold;

            // Buscar trackers que están marcados como online pero no han enviado datos recientemente
            var trackersToMarkOffline = await context.Trackers
                .Where(t => t.IsOnline && t.LastSeen < offlineThreshold)
                .ToListAsync();

            if (trackersToMarkOffline.Any())
            {
                foreach (var tracker in trackersToMarkOffline)
                {
                    tracker.IsOnline = false;
                    _logger.LogWarning($"📴 Tracker {tracker.DeviceId} marcado como OFFLINE (última señal: {tracker.LastSeen:yyyy-MM-dd HH:mm:ss})");
                }

                await context.SaveChangesAsync();
                _logger.LogInformation($"✅ {trackersToMarkOffline.Count} tracker(s) actualizados a estado OFFLINE");
            }

            // Buscar trackers que están marcados como offline pero han enviado datos recientemente
            var trackersToMarkOnline = await context.Trackers
                .Where(t => !t.IsOnline && t.LastSeen >= offlineThreshold)
                .ToListAsync();

            if (trackersToMarkOnline.Any())
            {
                foreach (var tracker in trackersToMarkOnline)
                {
                    tracker.IsOnline = true;
                    _logger.LogInformation($"📡 Tracker {tracker.DeviceId} marcado como ONLINE (última señal: {tracker.LastSeen:yyyy-MM-dd HH:mm:ss})");
                }

                await context.SaveChangesAsync();
                _logger.LogInformation($"✅ {trackersToMarkOnline.Count} tracker(s) actualizados a estado ONLINE");
            }

            // Log estadísticas
            var totalTrackers = await context.Trackers.CountAsync();
            var onlineTrackers = await context.Trackers.CountAsync(t => t.IsOnline);
            var offlineTrackers = totalTrackers - onlineTrackers;

            _logger.LogDebug($"📊 Estado trackers: {onlineTrackers} online, {offlineTrackers} offline (de {totalTrackers} total)");
        }
    }
}
