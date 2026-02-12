using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class CreateWeightRecordDto
    {
        public int AnimalId { get; set; }
        [Required]
        public decimal Weight { get; set; }
        public DateTime WeightDate { get; set; } = DateTime.Today;
        public string? Notes { get; set; }
    }
}
