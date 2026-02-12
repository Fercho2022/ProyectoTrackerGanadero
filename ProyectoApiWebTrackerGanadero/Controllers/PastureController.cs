using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Helpers;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using ApiWebTrackerGanado.Services;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PastureController : ControllerBase
    {
        private readonly IPastureRepository _pastureRepository;
        private readonly PastureService _pastureService;
        private readonly IMapper _mapper;

        public PastureController(
            IPastureRepository pastureRepository,
            PastureService pastureService,
            IMapper mapper)
        {
            _pastureRepository = pastureRepository;
            _pastureService = pastureService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PastureDto>>> GetPastures([FromQuery] int farmId)
        {
            var pastures = await _pastureRepository.GetPasturesByFarmAsync(farmId);

            var pastureDtos = pastures.Select(p => new PastureDto
            {
                Id = p.Id,
                Name = p.Name,
                AreaCoordinates = GeometryHelper.ConvertPolygonToLatLng(p.Area),
                AreaSize = p.AreaSize,
                GrassType = p.GrassType,
                Capacity = p.Capacity,
                IsActive = p.IsActive,
                FarmId = p.FarmId
            }).ToList();

            return Ok(pastureDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PastureDto>> GetPasture(int id)
        {
            var pasture = await _pastureRepository.GetByIdAsync(id);
            if (pasture == null) return NotFound();

            var pastureDto = new PastureDto
            {
                Id = pasture.Id,
                Name = pasture.Name,
                AreaCoordinates = GeometryHelper.ConvertPolygonToLatLng(pasture.Area),
                AreaSize = pasture.AreaSize,
                GrassType = pasture.GrassType,
                Capacity = pasture.Capacity,
                IsActive = pasture.IsActive,
                FarmId = pasture.FarmId
            };

            return Ok(pastureDto);
        }

        [HttpPost]
        public async Task<ActionResult<PastureDto>> CreatePasture([FromBody] CreatePastureDto createPastureDto)
        {
            var polygon = GeometryHelper.CreatePolygonFromGoogleMapsCoordinates(createPastureDto.AreaCoordinates);

            var pasture = new Pasture
            {
                Name = createPastureDto.Name,
                Area = polygon,
                AreaSize = GeometryHelper.CalculatePolygonArea(polygon),
                GrassType = createPastureDto.GrassType,
                Capacity = createPastureDto.Capacity,
                FarmId = createPastureDto.FarmId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _pastureRepository.AddAsync(pasture);

            var pastureDto = new PastureDto
            {
                Id = pasture.Id,
                Name = pasture.Name,
                AreaCoordinates = createPastureDto.AreaCoordinates,
                AreaSize = pasture.AreaSize,
                GrassType = pasture.GrassType,
                Capacity = pasture.Capacity,
                IsActive = pasture.IsActive,
                FarmId = pasture.FarmId
            };

            return CreatedAtAction(nameof(GetPasture), new { id = pasture.Id }, pastureDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePasture(int id, [FromBody] CreatePastureDto updatePastureDto)
        {
            var pasture = await _pastureRepository.GetByIdAsync(id);
            if (pasture == null) return NotFound();

            var polygon = GeometryHelper.CreatePolygonFromGoogleMapsCoordinates(updatePastureDto.AreaCoordinates);

            pasture.Name = updatePastureDto.Name;
            pasture.Area = polygon;
            pasture.AreaSize = GeometryHelper.CalculatePolygonArea(polygon);
            pasture.GrassType = updatePastureDto.GrassType;
            pasture.Capacity = updatePastureDto.Capacity;

            await _pastureRepository.UpdateAsync(pasture);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePasture(int id)
        {
            var pasture = await _pastureRepository.GetByIdAsync(id);
            if (pasture == null) return NotFound();

            await _pastureRepository.DeleteAsync(pasture);
            return NoContent();
        }

        [HttpGet("{pastureId}/animals")]
        public async Task<ActionResult<IEnumerable<AnimalDto>>> GetAnimalsInPasture(int pastureId)
        {
            var pasture = await _pastureRepository.GetByIdAsync(pastureId);
            if (pasture == null) return NotFound();

            // This would require the animal repository to get animals in this specific pasture area
            // For now, return empty list - you'd implement this based on current animal locations
            return Ok(new List<AnimalDto>());
        }

        [HttpGet("usage-report")]
        public async Task<ActionResult<IEnumerable<PastureUsageDto>>> GetUsageReport(
            [FromQuery] int farmId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            var usage = await _pastureService.GetPastureUsageReportAsync(farmId, from, to);
            return Ok(usage);
        }

        [HttpGet("current-occupancy")]
        public async Task<ActionResult<IEnumerable<PastureUsageDto>>> GetCurrentOccupancy([FromQuery] int farmId)
        {
            var occupancy = await _pastureService.GetCurrentPastureOccupancyAsync(farmId);
            return Ok(occupancy);
        }
    }
}
