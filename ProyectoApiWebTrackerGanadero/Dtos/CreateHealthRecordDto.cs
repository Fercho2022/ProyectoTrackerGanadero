using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Dtos
{
    public class CreateHealthRecordDto
    {
        public int AnimalId { get; set; }
        [Required]
        public string RecordType { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        public DateTime RecordDate { get; set; }
        public string? VeterinarianName { get; set; }
        public string? Treatment { get; set; }
        public string? Medication { get; set; }
        public decimal? Cost { get; set; }
        public string? Notes { get; set; }
        public DateTime? NextCheckup { get; set; }
    }
}
