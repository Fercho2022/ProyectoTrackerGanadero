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
    public class WeightRecordsController : ControllerBase
    {
        private readonly IWeightRecordRepository _weightRecordRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public WeightRecordsController(
            IWeightRecordRepository weightRecordRepository,
            IAnimalRepository animalRepository,
            IMapper mapper)
        {
            _weightRecordRepository = weightRecordRepository;
            _animalRepository = animalRepository;
            _mapper = mapper;
        }

        [HttpGet("animal/{animalId}")]
        public async Task<ActionResult<IEnumerable<WeightRecord>>> GetAnimalWeightHistory(int animalId)
        {
            var weightRecords = await _weightRecordRepository.GetAnimalWeightHistoryAsync(animalId);
            return Ok(weightRecords);
        }

        [HttpGet("animal/{animalId}/latest")]
        public async Task<ActionResult<WeightRecord>> GetLatestWeightRecord(int animalId)
        {
            var weightRecord = await _weightRecordRepository.GetLatestWeightRecordAsync(animalId);
            if (weightRecord == null) return NotFound();

            return Ok(weightRecord);
        }

        [HttpGet("animal/{animalId}/weight-gain")]
        public async Task<ActionResult<object>> GetWeightGainAnalysis(int animalId, [FromQuery] int months = 6)
        {
            var averageGain = await _weightRecordRepository.GetAverageWeightGainAsync(animalId, months);
            var weightHistory = await _weightRecordRepository.GetAnimalWeightHistoryAsync(animalId);

            var analysis = new
            {
                AnimalId = animalId,
                AverageMonthlyGain = averageGain,
                TotalRecords = weightHistory.Count(),
                LatestWeight = weightHistory.FirstOrDefault()?.Weight ?? 0,
                FirstWeight = weightHistory.LastOrDefault()?.Weight ?? 0,
                TotalGain = (weightHistory.FirstOrDefault()?.Weight ?? 0) - (weightHistory.LastOrDefault()?.Weight ?? 0),
                AnalysisPeriod = $"Last {months} months"
            };

            return Ok(analysis);
        }

        [HttpPost]
        public async Task<ActionResult<WeightRecord>> CreateWeightRecord([FromBody] CreateWeightRecordDto createWeightRecordDto)
        {
            // Verify animal exists
            var animal = await _animalRepository.GetByIdAsync(createWeightRecordDto.AnimalId);
            if (animal == null)
                return BadRequest("Animal not found");

            var weightRecord = new WeightRecord
            {
                AnimalId = createWeightRecordDto.AnimalId,
                Weight = createWeightRecordDto.Weight,
                WeightDate = createWeightRecordDto.WeightDate,
                Notes = createWeightRecordDto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            await _weightRecordRepository.AddAsync(weightRecord);

            // Update animal's current weight
            animal.Weight = createWeightRecordDto.Weight;
            await _animalRepository.UpdateAsync(animal);

            return CreatedAtAction(nameof(GetAnimalWeightHistory), new { animalId = weightRecord.AnimalId }, weightRecord);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWeightRecord(int id, [FromBody] UpdateWeightRecordDto updateWeightRecordDto)
        {
            var weightRecord = await _weightRecordRepository.GetByIdAsync(id);
            if (weightRecord == null) return NotFound();

            weightRecord.Weight = updateWeightRecordDto.Weight;
            weightRecord.WeightDate = updateWeightRecordDto.WeightDate;
            weightRecord.Notes = updateWeightRecordDto.Notes;

            await _weightRecordRepository.UpdateAsync(weightRecord);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWeightRecord(int id)
        {
            var weightRecord = await _weightRecordRepository.GetByIdAsync(id);
            if (weightRecord == null) return NotFound();

            await _weightRecordRepository.DeleteAsync(weightRecord);
            return NoContent();
        }
    }
}
