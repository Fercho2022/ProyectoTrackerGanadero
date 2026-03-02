using TrackerGanadero.Shared.Models;

namespace TrackerGanadero.Shared.Services
{
    public class NavigationInstruction
    {
        public string Text { get; set; } = string.Empty;
        public double DistanceFromStart { get; set; }
        public CoordinateDto Location { get; set; } = new();
        public bool HasBeenAnnounced { get; set; } = false;
    }

    public class VoiceNavigationService
    {
        private readonly ITextToSpeechService _textToSpeech;
        private List<NavigationInstruction> _instructions = new();
        private bool _isEnabled = true;
        private bool _isSpeaking = false;
        private bool _hasAnnouncedStart = false;

        public VoiceNavigationService(ITextToSpeechService textToSpeech)
        {
            _textToSpeech = textToSpeech;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public List<NavigationInstruction> GenerateInstructions(RouteDto route, bool isUpdate = false)
        {
            _instructions = new List<NavigationInstruction>();

            if (route == null || route.Coordinates.Count < 2)
                return _instructions;

            var initialBearing = CalculateBearing(
                route.Coordinates[0].Latitude, route.Coordinates[0].Longitude,
                route.Coordinates[1].Latitude, route.Coordinates[1].Longitude
            );
            var cardinalDirection = GetCardinalDirection(initialBearing);

            if (!isUpdate && !_hasAnnouncedStart)
            {
                _instructions.Add(new NavigationInstruction
                {
                    Text = $"Iniciando navegacion. Dirijase hacia el {cardinalDirection}",
                    DistanceFromStart = 0,
                    Location = route.Coordinates[0]
                });
                _hasAnnouncedStart = true;
            }

            double cumulativeDistance = 0;
            const double TURN_DETECTION_THRESHOLD = 30.0;

            for (int i = 1; i < route.Coordinates.Count - 1; i++)
            {
                var prev = route.Coordinates[i - 1];
                var current = route.Coordinates[i];
                var next = route.Coordinates[i + 1];

                var segmentDistance = CalculateDistance(prev.Latitude, prev.Longitude, current.Latitude, current.Longitude);
                cumulativeDistance += segmentDistance;

                var bearing1 = CalculateBearing(prev.Latitude, prev.Longitude, current.Latitude, current.Longitude);
                var bearing2 = CalculateBearing(current.Latitude, current.Longitude, next.Latitude, next.Longitude);
                var bearingChange = NormalizeBearing(bearing2 - bearing1);

                if (Math.Abs(bearingChange) > TURN_DETECTION_THRESHOLD)
                {
                    var direction = GetTurnDirection(bearingChange);
                    var distanceToNext = CalculateDistance(current.Latitude, current.Longitude, next.Latitude, next.Longitude);

                    _instructions.Add(new NavigationInstruction
                    {
                        Text = $"{direction} en {FormatDistance(distanceToNext)}",
                        DistanceFromStart = cumulativeDistance,
                        Location = current
                    });
                }
            }

            var lastSegmentDistance = CalculateDistance(
                route.Coordinates[route.Coordinates.Count - 2].Latitude,
                route.Coordinates[route.Coordinates.Count - 2].Longitude,
                route.Coordinates[route.Coordinates.Count - 1].Latitude,
                route.Coordinates[route.Coordinates.Count - 1].Longitude
            );
            cumulativeDistance += lastSegmentDistance;

            _instructions.Add(new NavigationInstruction
            {
                Text = "Ha llegado a su destino",
                DistanceFromStart = cumulativeDistance,
                Location = route.Coordinates[route.Coordinates.Count - 1]
            });

            return _instructions;
        }

        public async Task CheckAndAnnounceInstructions(CoordinateDto userPosition, double traveledDistance)
        {
            if (!_isEnabled || _isSpeaking)
                return;

            var nextInstruction = _instructions.FirstOrDefault(i => !i.HasBeenAnnounced);
            if (nextInstruction == null)
                return;

            var distanceToInstruction = CalculateDistance(
                userPosition.Latitude, userPosition.Longitude,
                nextInstruction.Location.Latitude, nextInstruction.Location.Longitude
            );

            if (distanceToInstruction < 30 || traveledDistance >= nextInstruction.DistanceFromStart)
            {
                await AnnounceInstruction(nextInstruction);
            }
        }

        public async Task AnnounceArrival()
        {
            if (!_isEnabled || _isSpeaking)
                return;

            await SpeakAsync("Ha llegado a su destino");
        }

        public void CancelSpeech()
        {
            _isSpeaking = false;
        }

        public void Reset()
        {
            CancelSpeech();
            _instructions.Clear();
            _hasAnnouncedStart = false;
        }

        private async Task AnnounceInstruction(NavigationInstruction instruction)
        {
            instruction.HasBeenAnnounced = true;
            await SpeakAsync(instruction.Text);
        }

        private async Task SpeakAsync(string text)
        {
            try
            {
                _isSpeaking = true;
                await _textToSpeech.SpeakAsync(text, "es");
                _isSpeaking = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error speaking: {ex.Message}");
                _isSpeaking = false;
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            var dLon = (lon2 - lon1) * Math.PI / 180;
            lat1 = lat1 * Math.PI / 180;
            lat2 = lat2 * Math.PI / 180;
            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            var bearing = Math.Atan2(y, x) * 180 / Math.PI;
            return NormalizeBearing(bearing);
        }

        private double NormalizeBearing(double bearing)
        {
            while (bearing > 180) bearing -= 360;
            while (bearing < -180) bearing += 360;
            return bearing;
        }

        private string GetCardinalDirection(double bearing)
        {
            var normalizedBearing = bearing;
            while (normalizedBearing < 0) normalizedBearing += 360;
            while (normalizedBearing >= 360) normalizedBearing -= 360;

            if (normalizedBearing >= 337.5 || normalizedBearing < 22.5) return "Norte";
            else if (normalizedBearing < 67.5) return "Noreste";
            else if (normalizedBearing < 112.5) return "Este";
            else if (normalizedBearing < 157.5) return "Sureste";
            else if (normalizedBearing < 202.5) return "Sur";
            else if (normalizedBearing < 247.5) return "Suroeste";
            else if (normalizedBearing < 292.5) return "Oeste";
            else return "Noroeste";
        }

        private string GetTurnDirection(double bearingChange)
        {
            if (bearingChange > 150) return "De la vuelta";
            else if (bearingChange > 45) return "Gire a la derecha";
            else if (bearingChange > 15) return "Gire ligeramente a la derecha";
            else if (bearingChange < -150) return "De la vuelta";
            else if (bearingChange < -45) return "Gire a la izquierda";
            else if (bearingChange < -15) return "Gire ligeramente a la izquierda";
            else return "Continue recto";
        }

        private string FormatDistance(double meters)
        {
            if (meters < 50) return $"{Math.Round(meters / 10) * 10} metros";
            else if (meters < 1000) return $"{Math.Round(meters / 50) * 50} metros";
            else return $"{meters / 1000:F1} kilometros";
        }
    }
}
