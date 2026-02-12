using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    /// <summary>
    /// Modelo para gestionar licencias/claves de activación por cliente
    /// </summary>
    public class License
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        /// <summary>
        /// Clave única de licencia generada por el desarrollador
        /// Formato: TG-YYYY-XXXX-XXXX-XXXX (Tracker Ganadero - Año - Código)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string LicenseKey { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de licencia (Basic, Premium, Enterprise)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string LicenseType { get; set; } = "Basic";

        /// <summary>
        /// Número máximo de trackers que permite esta licencia
        /// </summary>
        public int MaxTrackers { get; set; } = 10;

        /// <summary>
        /// Número máximo de granjas que permite esta licencia
        /// </summary>
        public int MaxFarms { get; set; } = 1;

        /// <summary>
        /// Número máximo de usuarios que permite esta licencia
        /// </summary>
        public int MaxUsers { get; set; } = 1;

        /// <summary>
        /// Funcionalidades habilitadas (JSON string)
        /// Ejemplo: {"realTimeMap": true, "reports": true, "alerts": true}
        /// </summary>
        [StringLength(1000)]
        public string? Features { get; set; }

        /// <summary>
        /// Estado de la licencia (Active, Suspended, Expired, Revoked)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active";

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ActivatedAt { get; set; }
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddYears(1);

        /// <summary>
        /// IP desde donde se activó la licencia
        /// </summary>
        [StringLength(50)]
        public string? ActivationIp { get; set; }

        /// <summary>
        /// Identificador de hardware/máquina donde se activó
        /// </summary>
        [StringLength(100)]
        public string? HardwareId { get; set; }

        /// <summary>
        /// Notas adicionales del desarrollador
        /// </summary>
        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Verifica si la licencia está activa y vigente
        /// </summary>
        public bool IsValid()
        {
            return Status == "Active" &&
                   ExpiresAt > DateTime.UtcNow &&
                   ActivatedAt.HasValue;
        }

        /// <summary>
        /// Verifica si la licencia puede ser activada
        /// </summary>
        public bool CanBeActivated()
        {
            return Status == "Active" &&
                   ExpiresAt > DateTime.UtcNow &&
                   !ActivatedAt.HasValue;
        }

        /// <summary>
        /// Activa la licencia con información de activación
        /// </summary>
        public void Activate(string ipAddress, string? hardwareId = null)
        {
            if (!CanBeActivated())
                throw new InvalidOperationException("La licencia no puede ser activada");

            ActivatedAt = DateTime.UtcNow;
            ActivationIp = ipAddress;
            HardwareId = hardwareId;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Genera una clave de licencia única
        /// </summary>
        public static string GenerateLicenseKey()
        {
            var year = DateTime.UtcNow.Year;
            var random = new Random();
            var part1 = random.Next(1000, 9999);
            var part2 = random.Next(1000, 9999);
            var part3 = random.Next(1000, 9999);

            return $"TG-{year}-{part1}-{part2}-{part3}";
        }
    }
}