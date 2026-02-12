using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Helpers;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Models;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FarmsController : ControllerBase
    {
        private readonly IFarmRepository _farmRepository;
        private readonly IMapper _mapper;

        public FarmsController(IFarmRepository farmRepository, IMapper mapper)
        {
            _farmRepository = farmRepository;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FarmDto>>> GetFarms([FromQuery] int? userId)
        {
            // SEGURIDAD: Siempre filtrar por el usuario actual del token JWT
            // NO permitir ver granjas de otros usuarios
            var currentUserId = GetCurrentUserId();

            IEnumerable<Farm> farms;

            // IMPORTANTE: Ignorar el parámetro userId y siempre usar el userId del token
            // Esto previene que un usuario vea granjas de otros usuarios
            farms = await _farmRepository.GetFarmsByUserWithBoundariesAsync(currentUserId);

            var farmDtos = farms.Select(f => new FarmDto
            {
                Id = f.Id,
                Name = f.Name,
                Address = f.Address,
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                UserId = f.UserId,
                CreatedAt = f.CreatedAt,
                BoundaryCoordinates = f.BoundaryCoordinates?.OrderBy(b => b.SequenceOrder)
                    .Select(b => new LatLngDto { Lat = b.Latitude, Lng = b.Longitude })
                    .ToList() ?? new List<LatLngDto>()
            }).ToList();

            return Ok(farmDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<FarmDto>> GetFarm(int id)
        {
            var farm = await _farmRepository.GetByIdWithBoundariesAsync(id);
            if (farm == null) return NotFound();

            // SEGURIDAD: Verificar que la granja pertenezca al usuario actual
            var currentUserId = GetCurrentUserId();
            if (farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden - Usuario no tiene permiso
            }

            var farmDto = new FarmDto
            {
                Id = farm.Id,
                Name = farm.Name,
                Address = farm.Address,
                Latitude = farm.Latitude,
                Longitude = farm.Longitude,
                UserId = farm.UserId,
                CreatedAt = farm.CreatedAt,
                BoundaryCoordinates = farm.BoundaryCoordinates?.OrderBy(b => b.SequenceOrder)
                    .Select(b => new LatLngDto { Lat = b.Latitude, Lng = b.Longitude })
                    .ToList() ?? new List<LatLngDto>()
            };

            return Ok(farmDto);
        }

        [HttpPost]
        public async Task<ActionResult<FarmDto>> CreateFarm([FromBody] CreateFarmDto createFarmDto)
        {
            var farm = new Farm
            {
                Name = createFarmDto.Name,
                Address = createFarmDto.Address ?? createFarmDto.Description,
                // Note: Latitude, Longitude and Boundaries are temporarily disabled with [NotMapped]
                Latitude = createFarmDto.Latitude,
                Longitude = createFarmDto.Longitude,
                UserId = GetCurrentUserId(),
                CreatedAt = DateTime.UtcNow
            };

            await _farmRepository.AddAsync(farm);

            var farmDto = new FarmDto
            {
                Id = farm.Id,
                Name = farm.Name,
                Address = farm.Address,
                Latitude = farm.Latitude,
                Longitude = farm.Longitude,
                UserId = farm.UserId,
                CreatedAt = farm.CreatedAt,
                BoundaryCoordinates = new List<LatLngDto>()
            };

            return CreatedAtAction(nameof(GetFarm), new { id = farm.Id }, farmDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFarm(int id, [FromBody] CreateFarmDto updateFarmDto)
        {
            var farm = await _farmRepository.GetByIdAsync(id);
            if (farm == null) return NotFound();

            // SEGURIDAD: Verificar que la granja pertenezca al usuario actual
            var currentUserId = GetCurrentUserId();
            if (farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden - Usuario no puede editar granjas ajenas
            }

            farm.Name = updateFarmDto.Name;
            farm.Address = updateFarmDto.Address;

            // ✅ FIXED: Update central coordinates
            farm.Latitude = updateFarmDto.Latitude;
            farm.Longitude = updateFarmDto.Longitude;

            if (updateFarmDto.BoundaryCoordinates?.Any() == true)
            {
                // Clear existing boundaries
                await _farmRepository.ClearFarmBoundariesAsync(farm.Id);

                // Add new boundaries
                await _farmRepository.SetFarmBoundariesAsync(farm.Id, updateFarmDto.BoundaryCoordinates);
            }

            await _farmRepository.UpdateAsync(farm);

            return NoContent();
        }

        [HttpPut("{farmId}/boundaries")]
        public async Task<ActionResult<bool>> UpdateFarmBoundaries(int farmId, [FromBody] List<LatLngDto> boundaries)
        {
            var farm = await _farmRepository.GetByIdAsync(farmId);
            if (farm == null) return NotFound();

            // SEGURIDAD: Verificar que la granja pertenezca al usuario actual
            var currentUserId = GetCurrentUserId();
            if (farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden - Usuario no puede editar límites de granjas ajenas
            }

            // Clear existing boundaries
            await _farmRepository.ClearFarmBoundariesAsync(farmId);

            if (boundaries?.Any() == true)
            {
                // Add new boundaries
                await _farmRepository.SetFarmBoundariesAsync(farmId, boundaries);
            }

            return Ok(true);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFarm(int id)
        {
            var farm = await _farmRepository.GetByIdAsync(id);
            if (farm == null) return NotFound();

            // SEGURIDAD CRÍTICA: Verificar que la granja pertenezca al usuario actual
            // ESTO PREVIENE que un usuario elimine granjas de otros usuarios
            var currentUserId = GetCurrentUserId();
            if (farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden - Usuario no puede eliminar granjas ajenas
            }

            await _farmRepository.DeleteAsync(farm);
            return NoContent();
        }

        [HttpGet("{farmId}/animals")]
        public async Task<ActionResult<IEnumerable<AnimalDto>>> GetFarmAnimals(int farmId)
        {
            var farm = await _farmRepository.GetFarmWithAnimalsAsync(farmId);
            if (farm == null) return NotFound();

            // SEGURIDAD: Verificar que la granja pertenezca al usuario actual
            var currentUserId = GetCurrentUserId();
            if (farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden - Usuario no puede ver animales de granjas ajenas
            }

            var animalDtos = _mapper.Map<List<AnimalDto>>(farm.Animals);
            return Ok(animalDtos);
        }

        private int GetCurrentUserId()
        {
            // The 'sub' claim from the JWT is often mapped to NameIdentifier
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                // Fallback to checking for 'sub' directly, in case mapping is disabled
                userIdClaim = User.FindFirst("sub")?.Value;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            // Original code defaulted to 1, which hides authentication issues.
            // Throwing an exception is cleaner. A user must be authenticated to create a farm.
            throw new UnauthorizedAccessException("Could not determine user ID from token claims.");
        }
    }
}
