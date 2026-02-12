using ApiWebTrackerGanado.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DebugController : ControllerBase
    {
        private readonly CattleTrackingContext _context;

        public DebugController(CattleTrackingContext context)
        {
            _context = context;
        }

        [HttpPost("update-animal-statuses")]
        public async Task<IActionResult> UpdateAnimalStatuses()
        {
            try
            {
                // Actualizar animales específicos a diferentes estados
                var animal3 = await _context.Animals.FindAsync(3);
                var animal6 = await _context.Animals.FindAsync(6);
                var animal9 = await _context.Animals.FindAsync(9);
                var animal4 = await _context.Animals.FindAsync(4);
                var animal7 = await _context.Animals.FindAsync(7);

                var results = new List<string>();

                if (animal3 != null) { animal3.Status = "Sick"; results.Add($"Animal 3: {animal3.Name} -> Sick"); }
                if (animal6 != null) { animal6.Status = "Sick"; results.Add($"Animal 6: {animal6.Name} -> Sick"); }
                if (animal9 != null) { animal9.Status = "Sick"; results.Add($"Animal 9: {animal9.Name} -> Sick"); }
                if (animal4 != null) { animal4.Status = "Monitoring"; results.Add($"Animal 4: {animal4.Name} -> Monitoring"); }
                if (animal7 != null) { animal7.Status = "Monitoring"; results.Add($"Animal 7: {animal7.Name} -> Monitoring"); }

                // Actualizar algunos otros a Healthy
                var healthyIds = new[] { 5, 8, 10, 11, 12, 13 };
                foreach (var id in healthyIds)
                {
                    var animal = await _context.Animals.FindAsync(id);
                    if (animal != null)
                    {
                        animal.Status = "Healthy";
                        results.Add($"Animal {id}: {animal.Name} -> Healthy");
                    }
                }

                await _context.SaveChangesAsync();

                // Verificar cambios
                var allAnimals = await _context.Animals.ToListAsync();
                var statusSummary = allAnimals.GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToList();

                return Ok(new
                {
                    Message = "Estados actualizados exitosamente",
                    Updates = results,
                    StatusSummary = statusSummary,
                    AllAnimals = allAnimals.Select(a => new { a.Id, a.Name, a.Status }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}