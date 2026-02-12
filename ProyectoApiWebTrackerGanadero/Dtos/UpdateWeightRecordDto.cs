using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class UpdateWeightRecordDto
    {
        [Required]
        public decimal Weight { get; set; }
        public DateTime WeightDate { get; set; }
        public string? Notes { get; set; }
    }
}
