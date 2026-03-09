using System.ComponentModel.DataAnnotations;

namespace ApiWebTrackerGanado.Models
{
    public class AnimalActivityBaseline
    {
        public int Id { get; set; }

        public int AnimalId { get; set; }
        public Animal Animal { get; set; } = null!;

        /// <summary>
        /// Dia calendario del registro
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Distancia total recorrida ese dia (metros), calculada con Haversine
        /// </summary>
        public double DailyDistanceMeters { get; set; }

        /// <summary>
        /// Distancia promedio al toro en metros. -1 si no hay toro en la granja.
        /// </summary>
        public double AverageProximityToToro { get; set; } = -1;

        /// <summary>
        /// Cantidad de muestras GPS usadas para calcular la distancia
        /// </summary>
        public int LocationSamples { get; set; }
    }
}
