using ApiWebTrackerGanado.Interfaces;

namespace ApiWebTrackerGanado.Services.BackgroundServices
{
    public class AlertProcessingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AlertProcessingService> _logger;

        public AlertProcessingService(IServiceProvider serviceProvider, ILogger<AlertProcessingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var pastureService = scope.ServiceProvider.GetRequiredService<IPastureService>();

                    // Update pasture usage every 5 minutes
                    await pastureService.UpdatePastureUsageAsync();

                    _logger.LogInformation("Alert processing completed at {time}", DateTimeOffset.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during alert processing");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}

    

