using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BreedingController : ControllerBase
    {
        private readonly IBreedingRecordRepository _breedingRecordRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;
        private readonly CattleTrackingContext _context;
        private readonly IAlertService _alertService;
        private readonly IFarmRepository _farmRepository;

        public BreedingController(
            IBreedingRecordRepository breedingRecordRepository,
            IAnimalRepository animalRepository,
            IMapper mapper,
            CattleTrackingContext context,
            IAlertService alertService,
            IFarmRepository farmRepository)
        {
            _breedingRecordRepository = breedingRecordRepository;
            _animalRepository = animalRepository;
            _mapper = mapper;
            _context = context;
            _alertService = alertService;
            _farmRepository = farmRepository;
        }

        [HttpGet("animal/{animalId}")]
        public async Task<ActionResult<IEnumerable<BreedingRecord>>> GetAnimalBreedingHistory(int animalId)
        {
            var breedingRecords = await _breedingRecordRepository.GetAnimalBreedingHistoryAsync(animalId);
            return Ok(breedingRecords);
        }

        [HttpPost]
        public async Task<ActionResult<BreedingRecord>> CreateBreedingRecord([FromBody] BreedingRecord breedingRecord)
        {
            breedingRecord.CreatedAt = DateTime.UtcNow;
            await _breedingRecordRepository.AddAsync(breedingRecord);

            return CreatedAtAction(nameof(GetAnimalBreedingHistory), new { animalId = breedingRecord.AnimalId }, breedingRecord);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBreedingRecord(int id, [FromBody] BreedingRecord breedingRecord)
        {
            var existing = await _breedingRecordRepository.GetByIdAsync(id);
            if (existing == null) return NotFound();

            existing.EventType = breedingRecord.EventType;
            existing.EventDate = breedingRecord.EventDate;
            existing.ExpectedBirthDate = breedingRecord.ExpectedBirthDate;
            existing.ActualBirthDate = breedingRecord.ActualBirthDate;
            existing.OffspringCount = breedingRecord.OffspringCount;
            existing.Notes = breedingRecord.Notes;

            await _breedingRecordRepository.UpdateAsync(existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBreedingRecord(int id)
        {
            var breedingRecord = await _breedingRecordRepository.GetByIdAsync(id);
            if (breedingRecord == null) return NotFound();

            await _breedingRecordRepository.DeleteAsync(breedingRecord);
            return NoContent();
        }

        [HttpGet("farm/{farmId}/expected-births")]
        public async Task<ActionResult<IEnumerable<BreedingRecord>>> GetExpectedBirths(
            int farmId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            var expectedBirths = await _breedingRecordRepository.GetExpectedBirthsAsync(farmId, from, to);
            return Ok(expectedBirths);
        }

        [HttpGet("farm/{farmId}/recent-heat")]
        public async Task<ActionResult<IEnumerable<BreedingRecord>>> GetRecentHeatEvents(
            int farmId,
            [FromQuery] int daysBack = 7)
        {
            var heatEvents = await _breedingRecordRepository.GetRecentHeatEventsAsync(farmId, daysBack);
            return Ok(heatEvents);
        }

        [HttpGet("farm/{farmId}/breeding-females")]
        public async Task<ActionResult<IEnumerable<AnimalDto>>> GetBreedingFemales(int farmId)
        {
            var breedingFemales = await _animalRepository.GetBreedingFemalesAsync(farmId);
            var animalDtos = _mapper.Map<List<AnimalDto>>(breedingFemales);
            return Ok(animalDtos);
        }

        /// <summary>
        /// Inyecta baselines de prueba para testing rapido de deteccion de celo.
        /// Crea 7 dias de distancia normal (3-5 km/dia) para todas las hembras de una granja.
        /// </summary>
        [HttpPost("seed-test-baselines/{farmId}")]
        public async Task<IActionResult> SeedTestBaselines(int farmId)
        {
            var breedingFemales = await _animalRepository.GetBreedingFemalesAsync(farmId);
            var today = DateTime.UtcNow.Date;
            var rng = new Random();
            int seeded = 0;

            foreach (var animal in breedingFemales)
            {
                for (int day = 1; day <= 7; day++)
                {
                    var date = today.AddDays(-day);
                    var exists = await _context.AnimalActivityBaselines
                        .AnyAsync(b => b.AnimalId == animal.Id && b.Date == date);

                    if (!exists)
                    {
                        _context.AnimalActivityBaselines.Add(new AnimalActivityBaseline
                        {
                            AnimalId = animal.Id,
                            Date = date,
                            DailyDistanceMeters = rng.NextDouble() * 2000 + 3000, // 3000-5000m
                            AverageProximityToToro = rng.NextDouble() * 400 + 200,  // 200-600m (lejos)
                            LocationSamples = rng.Next(200, 500)
                        });
                        seeded++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                Message = $"Baselines de prueba inyectados",
                AnimalsProcessed = breedingFemales.Count(),
                RecordsCreated = seeded,
                DaysSeeded = 7
            });
        }

        /// <summary>
        /// Fuerza ejecucion inmediata del analisis de celo para todas las hembras de una granja.
        /// Util para testing sin esperar el ciclo de 4h del BreedingAnalysisService.
        /// </summary>
        [HttpPost("analyze-now/{farmId}")]
        public async Task<IActionResult> ForceAnalysis(int farmId)
        {
            var breedingFemales = await _animalRepository.GetBreedingFemalesAsync(farmId);
            int analyzed = 0;
            var results = new List<string>();

            foreach (var animal in breedingFemales)
            {
                try
                {
                    await _alertService.CheckBreedingAlertsAsync(animal);
                    analyzed++;
                }
                catch (Exception ex)
                {
                    results.Add($"{animal.Name}: Error - {ex.Message}");
                }
            }

            return Ok(new
            {
                Message = $"Analisis de celo completado",
                AnimalsAnalyzed = analyzed,
                Errors = results
            });
        }

        [HttpGet("farm/{farmId}/breeding-summary")]
        public async Task<ActionResult<object>> GetBreedingSummary(int farmId)
        {
            var breedingFemales = await _animalRepository.GetBreedingFemalesAsync(farmId);
            var recentHeat = await _breedingRecordRepository.GetRecentHeatEventsAsync(farmId, 30);
            var expectedBirths = await _breedingRecordRepository.GetExpectedBirthsAsync(farmId, DateTime.Today, DateTime.Today.AddMonths(3));

            var summary = new
            {
                TotalBreedingFemales = breedingFemales.Count(),
                RecentHeatEvents = recentHeat.Count(),
                ExpectedBirthsNext3Months = expectedBirths.Count(),
                BreedingRate = breedingFemales.Any() ? (double)recentHeat.Count() / breedingFemales.Count() * 100 : 0
            };

            return Ok(summary);
        }
    }
}
