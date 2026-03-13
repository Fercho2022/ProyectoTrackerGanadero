using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Services.BackgroundServices
{
    public class BreedingAnalysisService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BreedingAnalysisService> _logger;
        private const int BASELINE_RETENTION_DAYS = 14; // Mantener 2 semanas de historial

        public BreedingAnalysisService(IServiceProvider serviceProvider, ILogger<BreedingAnalysisService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Esperar 2 minutos al iniciar para que la BD y otros servicios esten listos
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var farmRepository = scope.ServiceProvider.GetRequiredService<IFarmRepository>();
                    var animalRepository = scope.ServiceProvider.GetRequiredService<IAnimalRepository>();
                    var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                    var context = scope.ServiceProvider.GetRequiredService<CattleTrackingContext>();

                    // Analizar todas las hembras reproductivas de todas las granjas
                    var farms = await farmRepository.GetAllAsync();
                    int animalsAnalyzed = 0;

                    foreach (var farm in farms)
                    {
                        var breedingAnimals = await animalRepository.GetBreedingFemalesAsync(farm.Id);

                        foreach (var animal in breedingAnimals)
                        {
                            try
                            {
                                await alertService.CheckBreedingAlertsAsync(animal);
                                animalsAnalyzed++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error analyzing breeding for animal {AnimalId} ({AnimalName})",
                                    animal.Id, animal.Name);
                            }
                        }
                    }

                    // Limpiar baselines viejos (> 14 dias)
                    var cutoffDate = DateTime.UtcNow.Date.AddDays(-BASELINE_RETENTION_DAYS);
                    var oldBaselines = await context.AnimalActivityBaselines
                        .Where(b => b.Date < cutoffDate)
                        .ToListAsync(stoppingToken);

                    if (oldBaselines.Any())
                    {
                        context.AnimalActivityBaselines.RemoveRange(oldBaselines);
                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Cleaned up {Count} old activity baselines (older than {Days} days)",
                            oldBaselines.Count, BASELINE_RETENTION_DAYS);
                    }

                    _logger.LogInformation(
                        "Breeding analysis completed: {Count} animals analyzed at {Time}",
                        animalsAnalyzed, DateTimeOffset.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during breeding analysis");
                }

                // Ejecutar cada 4 horas en produccion
                // Para testing rapido con emulador celo_test_1, cambiar a: TimeSpan.FromMinutes(2)
                await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
            }
        }
    }
}
