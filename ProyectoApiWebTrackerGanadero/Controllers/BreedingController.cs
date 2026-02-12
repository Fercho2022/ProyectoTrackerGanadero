using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BreedingController : ControllerBase
    {
        private readonly IBreedingRecordRepository _breedingRecordRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public BreedingController(
            IBreedingRecordRepository breedingRecordRepository,
            IAnimalRepository animalRepository,
            IMapper mapper)
        {
            _breedingRecordRepository = breedingRecordRepository;
            _animalRepository = animalRepository;
            _mapper = mapper;
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
