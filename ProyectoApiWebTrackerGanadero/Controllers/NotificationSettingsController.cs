using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize] // TODO: Re-enable authentication when implemented properly
    public class NotificationSettingsController : ControllerBase
    {
        private readonly CattleTrackingContext _context;

        public NotificationSettingsController(CattleTrackingContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<NotificationSettingsDto>> GetSettings()
        {
            var userId = GetCurrentUserId();
            var settings = await _context.NotificationSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(ns => ns.UserId == userId);

            if (settings == null)
            {
                return Ok(new NotificationSettingsDto());
            }

            return Ok(MapToDto(settings));
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] NotificationSettingsDto dto)
        {
            var userId = GetCurrentUserId();
            var settings = await _context.NotificationSettings
                .FirstOrDefaultAsync(ns => ns.UserId == userId);

            if (settings == null)
            {
                settings = new NotificationSettings { UserId = userId, CreatedAt = DateTime.UtcNow };
                _context.NotificationSettings.Add(settings);
            }

            settings.PhoneNumber = dto.PhoneNumber;
            settings.NotificationEmail = dto.NotificationEmail;
            settings.EnableEmailNotifications = dto.EnableEmailNotifications;
            settings.EnableWhatsAppNotifications = dto.EnableWhatsAppNotifications;
            settings.AlertNoSignal = dto.AlertNoSignal;
            settings.AlertWeakSignal = dto.AlertWeakSignal;
            settings.AlertAbruptDisconnection = dto.AlertAbruptDisconnection;
            settings.AlertNightMovement = dto.AlertNightMovement;
            settings.AlertSuddenExit = dto.AlertSuddenExit;
            settings.AlertUnusualSpeed = dto.AlertUnusualSpeed;
            settings.AlertTrackerManipulation = dto.AlertTrackerManipulation;
            settings.AlertOutOfBounds = dto.AlertOutOfBounds;
            settings.AlertImmobility = dto.AlertImmobility;
            settings.AlertLowActivity = dto.AlertLowActivity;
            settings.AlertHighActivity = dto.AlertHighActivity;
            settings.AlertPossibleHeat = dto.AlertPossibleHeat;
            settings.AlertBatteryLow = dto.AlertBatteryLow;
            settings.AlertBatteryCritical = dto.AlertBatteryCritical;
            settings.AlertInvalidCoordinates = dto.AlertInvalidCoordinates;
            settings.AlertLocationJump = dto.AlertLocationJump;
            settings.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static NotificationSettingsDto MapToDto(NotificationSettings s) => new()
        {
            PhoneNumber = s.PhoneNumber,
            NotificationEmail = s.NotificationEmail,
            EnableEmailNotifications = s.EnableEmailNotifications,
            EnableWhatsAppNotifications = s.EnableWhatsAppNotifications,
            AlertNoSignal = s.AlertNoSignal,
            AlertWeakSignal = s.AlertWeakSignal,
            AlertAbruptDisconnection = s.AlertAbruptDisconnection,
            AlertNightMovement = s.AlertNightMovement,
            AlertSuddenExit = s.AlertSuddenExit,
            AlertUnusualSpeed = s.AlertUnusualSpeed,
            AlertTrackerManipulation = s.AlertTrackerManipulation,
            AlertOutOfBounds = s.AlertOutOfBounds,
            AlertImmobility = s.AlertImmobility,
            AlertLowActivity = s.AlertLowActivity,
            AlertHighActivity = s.AlertHighActivity,
            AlertPossibleHeat = s.AlertPossibleHeat,
            AlertBatteryLow = s.AlertBatteryLow,
            AlertBatteryCritical = s.AlertBatteryCritical,
            AlertInvalidCoordinates = s.AlertInvalidCoordinates,
            AlertLocationJump = s.AlertLocationJump
        };

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

            // Fallback para desarrollo sin autenticacion
            return 1;
        }
    }
}
