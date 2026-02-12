using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(20)]
        public string Role { get; set; } = "User";

        public bool IsActive { get; set; } = true;

        public ICollection<Farm> Farms { get; set; } = new List<Farm>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
