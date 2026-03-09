using System.Net;
using System.Net.Mail;
using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiWebTrackerGanado.Services
{
    public class EmailNotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            ILogger<EmailNotificationService> logger)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task SendAlertNotificationAsync(int userId, Alert alert, string animalName)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CattleTrackingContext>();

                var settings = await context.NotificationSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ns => ns.UserId == userId);

                if (settings == null) return;

                // Verificar si el tipo de alerta esta habilitado
                if (!IsAlertTypeEnabled(settings, alert.Type)) return;

                // Enviar email si esta habilitado
                if (settings.EnableEmailNotifications)
                {
                    var email = settings.NotificationEmail;
                    if (string.IsNullOrEmpty(email))
                    {
                        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                        email = user?.Email;
                    }

                    if (!string.IsNullOrEmpty(email))
                    {
                        await SendEmailAsync(email, alert, animalName);
                    }
                }

                // WhatsApp: log para futura implementacion
                if (settings.EnableWhatsAppNotifications && !string.IsNullOrEmpty(settings.PhoneNumber))
                {
                    _logger.LogInformation(
                        "WhatsApp notification pending for user {UserId}, phone {Phone}, alert type {AlertType}",
                        userId, settings.PhoneNumber, alert.Type);
                    // TODO: Integrar con WhatsApp Business API (Twilio / Meta Cloud API)
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification for alert {AlertId} to user {UserId}", alert.Id, userId);
            }
        }

        private bool IsAlertTypeEnabled(NotificationSettings settings, string alertType)
        {
            return alertType switch
            {
                "NoSignal" => settings.AlertNoSignal,
                "WeakSignal" => settings.AlertWeakSignal,
                "AbruptDisconnection" => settings.AlertAbruptDisconnection,
                "NightMovement" => settings.AlertNightMovement,
                "SuddenExit" => settings.AlertSuddenExit,
                "UnusualSpeed" => settings.AlertUnusualSpeed,
                "TrackerManipulation" => settings.AlertTrackerManipulation,
                "OutOfBounds" => settings.AlertOutOfBounds,
                "Immobility" => settings.AlertImmobility,
                "LowActivity" => settings.AlertLowActivity,
                "HighActivity" => settings.AlertHighActivity,
                "PossibleHeat" => settings.AlertPossibleHeat,
                "BatteryLow" => settings.AlertBatteryLow,
                "BatteryCritical" => settings.AlertBatteryCritical,
                "InvalidCoordinates" => settings.AlertInvalidCoordinates,
                "LocationJump" => settings.AlertLocationJump,
                _ => true
            };
        }

        private async Task SendEmailAsync(string toEmail, Alert alert, string animalName)
        {
            var smtpHost = _configuration["SmtpSettings:Host"];
            var smtpPort = int.TryParse(_configuration["SmtpSettings:Port"], out var port) ? port : 587;
            var smtpUser = _configuration["SmtpSettings:Username"];
            var smtpPass = _configuration["SmtpSettings:Password"];
            var fromEmail = _configuration["SmtpSettings:FromEmail"];
            var fromName = _configuration["SmtpSettings:FromName"] ?? "Tracker Ganadero";
            var enableSsl = _configuration["SmtpSettings:EnableSsl"] != "false";

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
            {
                _logger.LogWarning("SMTP not configured. Skipping email notification to {Email}", toEmail);
                return;
            }

            var severityColor = alert.Severity switch
            {
                "Critical" => "#dc3545",
                "High" => "#fd7e14",
                "Medium" => "#ffc107",
                "Low" => "#17a2b8",
                _ => "#6c757d"
            };

            var subject = $"[Tracker Ganadero] {alert.Severity}: {alert.Type} - {animalName}";
            var body = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <div style='background-color: {severityColor}; color: white; padding: 15px; border-radius: 8px 8px 0 0;'>
        <h2 style='margin: 0;'>Alerta de Tracker Ganadero</h2>
    </div>
    <div style='border: 1px solid #ddd; padding: 20px; border-radius: 0 0 8px 8px;'>
        <p><strong>Animal:</strong> {animalName}</p>
        <p><strong>Tipo:</strong> {alert.Type}</p>
        <p><strong>Severidad:</strong> <span style='color: {severityColor}; font-weight: bold;'>{alert.Severity}</span></p>
        <p><strong>Mensaje:</strong> {alert.Message}</p>
        <p><strong>Fecha:</strong> {alert.CreatedAt:dd/MM/yyyy HH:mm:ss} UTC</p>
        <hr style='border: none; border-top: 1px solid #eee;' />
        <p style='color: #666; font-size: 12px;'>
            Este email fue enviado automaticamente por Tracker Ganadero.
            Podes configurar tus preferencias de notificacion en la app.
        </p>
    </div>
</body>
</html>";

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = enableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail ?? smtpUser, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Email notification sent to {Email} for alert {AlertType}", toEmail, alert.Type);
        }
    }
}
