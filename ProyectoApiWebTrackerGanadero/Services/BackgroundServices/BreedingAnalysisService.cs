using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;

namespace ApiWebTrackerGanado.Services.BackgroundServices
{
    public class BreedingAnalysisService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BreedingAnalysisService> _logger;

        public BreedingAnalysisService(IServiceProvider serviceProvider, ILogger<BreedingAnalysisService> logger)
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
                    var farmRepository = scope.ServiceProvider.GetRequiredService<IFarmRepository>();
                    var animalRepository = scope.ServiceProvider.GetRequiredService<IAnimalRepository>();
                    var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

                    // Get all farms
                    var farms = await farmRepository.GetAllAsync();

                    foreach (var farm in farms)
                    {
                        var breedingAnimals = await animalRepository.GetBreedingFemalesAsync(farm.Id);

                        foreach (var animal in breedingAnimals)
                        {
                            await alertService.CheckBreedingAlertsAsync(animal);
                        }
                    }

                    _logger.LogInformation("Breeding analysis completed at {time}", DateTimeOffset.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during breeding analysis");
                }

                // Run every 4 hours
                await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
            }
        }


    }
}
