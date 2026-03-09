using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Models;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize] // Deshabilitado temporalmente para desarrollo
    public class AnimalsController : ControllerBase
    {
        private readonly CattleTrackingContext _context;
        private readonly IMapper _mapper;

        public AnimalsController(CattleTrackingContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AnimalDto>>> GetAnimals([FromQuery] int? farmId)
        {
            // SEGURIDAD: Solo devolver animales de las granjas del usuario actual
            var currentUserId = GetCurrentUserId();

            var query = _context.Animals
                .Include(a => a.Farm) // Incluir Farm para filtrar por UserId
                .Where(a => a.Farm.UserId == currentUserId); // Filtrar por usuario

            if (farmId.HasValue)
                query = query.Where(a => a.FarmId == farmId.Value);

            var animals = await query
                .Include(a => a.Tracker)
                .ToListAsync();

            var animalDtos = _mapper.Map<List<AnimalDto>>(animals);

            // Poblar CurrentLocation con la última ubicación conocida de cada animal
            var animalIds = animals.Select(a => a.Id).ToList();
            var lastLocations = await _context.LocationHistories
                .Where(lh => lh.AnimalId.HasValue && animalIds.Contains(lh.AnimalId.Value))
                .GroupBy(lh => lh.AnimalId!.Value)
                .Select(g => g.OrderByDescending(lh => lh.Timestamp).First())
                .ToListAsync();

            var locationMap = lastLocations
                .Where(lh => lh.AnimalId.HasValue)
                .ToDictionary(lh => lh.AnimalId!.Value);

            foreach (var dto in animalDtos)
            {
                if (locationMap.TryGetValue(dto.Id, out var loc))
                {
                    dto.CurrentLocation = new LocationDto
                    {
                        Latitude = loc.Latitude,
                        Longitude = loc.Longitude,
                        Altitude = loc.Altitude,
                        Speed = loc.Speed,
                        ActivityLevel = loc.ActivityLevel,
                        Temperature = loc.Temperature,
                        Timestamp = loc.Timestamp,
                        HasSignal = dto.TrackerIsOnline
                    };
                }
            }

            return Ok(animalDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AnimalDto>> GetAnimal(int id)
        {
            var animal = await _context.Animals
                .Include(a => a.Tracker)
                .Include(a => a.Farm)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (animal == null)
                return NotFound();

            // SEGURIDAD: Verificar que el animal pertenezca a una granja del usuario actual
            var currentUserId = GetCurrentUserId();
            if (animal.Farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden - Usuario no puede ver animales de otros usuarios
            }

            var animalDto = _mapper.Map<AnimalDto>(animal);
            return Ok(animalDto);
        }

        [HttpPost]
        public async Task<ActionResult<AnimalDto>> CreateAnimal([FromBody] CreateAnimalDto createAnimalDto)
        {
            try
            {
                // Log incoming data
                Console.WriteLine($"[CreateAnimal] Received data: Name={createAnimalDto.Name}, FarmId={createAnimalDto.FarmId}, Gender={createAnimalDto.Gender}, Breed={createAnimalDto.Breed}, Status={createAnimalDto.Status}, Weight={createAnimalDto.Weight}");

                // SEGURIDAD CRÍTICA: Verificar que la granja existe Y pertenece al usuario actual
                var currentUserId = GetCurrentUserId();
                var farm = await _context.Farms.FirstOrDefaultAsync(f => f.Id == createAnimalDto.FarmId);

                if (farm == null)
                {
                    Console.WriteLine($"[CreateAnimal] ERROR: Farm with ID {createAnimalDto.FarmId} does not exist");
                    return BadRequest($"La granja con ID {createAnimalDto.FarmId} no existe.");
                }

                if (farm.UserId != currentUserId)
                {
                    Console.WriteLine($"[CreateAnimal] SECURITY: User {currentUserId} tried to create animal in farm {createAnimalDto.FarmId} owned by user {farm.UserId}");
                    return Forbid(); // 403 Forbidden - Usuario no puede crear animales en granjas ajenas
                }

                // Create animal manually to avoid AutoMapper issues
                var animal = new Animal
                {
                    Name = createAnimalDto.Name?.Trim() ?? "Unknown",
                    Tag = createAnimalDto.Tag?.Trim(),
                    BirthDate = DateTime.SpecifyKind(createAnimalDto.BirthDate, DateTimeKind.Utc), // Ensure UTC
                    Gender = createAnimalDto.Gender?.Trim() ?? "Unknown",
                    Breed = createAnimalDto.Breed?.Trim() ?? "Unknown",
                    Weight = createAnimalDto.Weight > 0 ? createAnimalDto.Weight : 100, // Default weight
                    Status = !string.IsNullOrEmpty(createAnimalDto.Status) ? createAnimalDto.Status.Trim() : "Active",
                    FarmId = createAnimalDto.FarmId,
                    TrackerId = null, // Don't assign tracker initially
                    CustomerTrackerId = null, // Don't assign customer tracker initially
                    CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                    UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                Console.WriteLine($"[CreateAnimal] Mapped animal: Name={animal.Name}, FarmId={animal.FarmId}, Gender={animal.Gender}, Breed={animal.Breed}, Status={animal.Status}, Weight={animal.Weight}");

                _context.Animals.Add(animal);
                await _context.SaveChangesAsync();

                Console.WriteLine($"[CreateAnimal] SUCCESS: Animal created with ID {animal.Id}");

                var animalDto = _mapper.Map<AnimalDto>(animal);
                return CreatedAtAction(nameof(GetAnimal), new { id = animal.Id }, animalDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CreateAnimal] ERROR: {ex.Message}");
                Console.WriteLine($"[CreateAnimal] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[CreateAnimal] Inner exception: {ex.InnerException.Message}");
                }
                throw; // Re-throw to let the error handling middleware handle it
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAnimal(int id, [FromBody] CreateAnimalDto updateAnimalDto)
        {
            var animal = await _context.Animals
                .Include(a => a.Farm)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (animal == null)
                return NotFound();

            // SEGURIDAD: Verificar que el animal pertenezca a una granja del usuario actual
            var currentUserId = GetCurrentUserId();
            if (animal.Farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden - Usuario no puede editar animales de otros usuarios
            }

            _mapper.Map(updateAnimalDto, animal);
            animal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAnimal(int id)
        {
            var animal = await _context.Animals
                .Include(a => a.Farm)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (animal == null)
                return NotFound();

            // SEGURIDAD CRÍTICA: Verificar que el animal pertenezca a una granja del usuario actual
            var currentUserId = GetCurrentUserId();
            if (animal.Farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden - Usuario no puede eliminar animales de otros usuarios
            }

            _context.Animals.Remove(animal);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}/weight-history")]
        public async Task<ActionResult<IEnumerable<WeightRecord>>> GetAnimalWeightHistory(int id)
        {
            // SEGURIDAD: Verificar que el animal pertenezca al usuario actual
            var animal = await _context.Animals
                .Include(a => a.Farm)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (animal == null)
                return NotFound();

            var currentUserId = GetCurrentUserId();
            if (animal.Farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden
            }

            var weightRecords = await _context.WeightRecords
                .Where(wr => wr.AnimalId == id)
                .OrderByDescending(wr => wr.WeightDate)
                .ToListAsync();

            return Ok(weightRecords);
        }

        [HttpPost("{id}/weight")]
        public async Task<ActionResult<WeightRecord>> AddWeightRecord(int id, [FromBody] WeightRecord weightRecord)
        {
            // SEGURIDAD: Verificar que el animal pertenezca al usuario actual
            var animal = await _context.Animals
                .Include(a => a.Farm)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (animal == null)
                return NotFound();

            var currentUserId = GetCurrentUserId();
            if (animal.Farm.UserId != currentUserId)
            {
                return Forbid(); // 403 Forbidden
            }

            weightRecord.AnimalId = id;
            weightRecord.CreatedAt = DateTime.UtcNow;

            _context.WeightRecords.Add(weightRecord);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAnimalWeightHistory), new { id }, weightRecord);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                userIdClaim = User.FindFirst("sub")?.Value;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            return 1; // Fallback para desarrollo sin autenticación
        }
    }
}

