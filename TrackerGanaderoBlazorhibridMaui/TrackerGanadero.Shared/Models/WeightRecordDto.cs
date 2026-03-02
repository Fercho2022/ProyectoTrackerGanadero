namespace TrackerGanadero.Shared.Models
{
    public class WeightRecordDto
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public decimal Weight { get; set; }
        public DateTime WeightDate { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AnimalName { get; set; }
    }
}