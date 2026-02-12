namespace ApiWebTrackerGanado.Dtos
{
    public class PastureDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<LatLngDto> AreaCoordinates { get; set; } = new();
        public double AreaSize { get; set; }
        public string GrassType { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public bool IsActive { get; set; }
        public int FarmId { get; set; }
    }
}
