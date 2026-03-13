using System.Globalization;
using System.Text;
using ApiWebTrackerGanado.Dtos;
using Microsoft.Extensions.Logging;

namespace ApiWebTrackerGanado.Services
{
    /// <summary>
    /// Parser del protocolo S168 de Shenzhen Rayoid Technology.
    /// Formato de trama: ID#IMEI#SN#Length#Content$
    /// </summary>
    public class S168ProtocolParser
    {
        private readonly ILogger<S168ProtocolParser> _logger;

        public S168ProtocolParser(ILogger<S168ProtocolParser> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Parsea una trama completa del protocolo S168.
        /// </summary>
        public S168Frame? ParseFrame(string rawFrame)
        {
            try
            {
                // Remover $ del final si existe
                rawFrame = rawFrame.TrimEnd('$').Trim();

                var parts = rawFrame.Split('#');
                if (parts.Length < 5)
                {
                    _logger.LogWarning("Trama S168 invalida: menos de 5 segmentos. Raw: {Raw}", rawFrame);
                    return null;
                }

                var frame = new S168Frame
                {
                    Id = parts[0],
                    Imei = parts[1],
                    Sn = parts[2],
                    Content = string.Join("#", parts.Skip(4)) // Todo despues del 4to # es contenido
                };

                // Parsear length (hex)
                if (int.TryParse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var length))
                {
                    frame.ContentLength = length;
                }

                // Determinar tipo de paquete
                frame.PacketType = DeterminePacketType(frame.Content);

                return frame;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parseando trama S168: {Raw}", rawFrame);
                return null;
            }
        }

        /// <summary>
        /// Determina el tipo de paquete a partir del contenido.
        /// </summary>
        private string DeterminePacketType(string content)
        {
            if (content.StartsWith("LOCA", StringComparison.OrdinalIgnoreCase)) return "LOCA";
            if (content.StartsWith("SYNC", StringComparison.OrdinalIgnoreCase)) return "SYNC";
            if (content.StartsWith("INFO", StringComparison.OrdinalIgnoreCase)) return "INFO";
            if (content.StartsWith("B2G", StringComparison.OrdinalIgnoreCase)) return "B2G";
            if (content.StartsWith("TKU", StringComparison.OrdinalIgnoreCase)) return "TKU";
            if (content.StartsWith("TKR", StringComparison.OrdinalIgnoreCase)) return "TKR";
            if (content.StartsWith("FORWARD", StringComparison.OrdinalIgnoreCase)) return "FORWARD";
            if (content.StartsWith("RADDR", StringComparison.OrdinalIgnoreCase)) return "RADDR";
            return "UNKNOWN";
        }

        /// <summary>
        /// Parsea un paquete LOCA (posicionamiento).
        /// Formato: LOCA:G;CELL:...;GDATA:...;ALERT:...;STATUS:...;WAY:...
        /// </summary>
        public S168LocaPacket? ParseLocaPacket(string content)
        {
            try
            {
                var packet = new S168LocaPacket();
                var sections = content.Split(';');

                foreach (var section in sections)
                {
                    var colonIndex = section.IndexOf(':');
                    if (colonIndex < 0) continue;

                    var keyword = section[..colonIndex].Trim().ToUpperInvariant();
                    var value = section[(colonIndex + 1)..].Trim();

                    switch (keyword)
                    {
                        case "LOCA":
                            packet.LocationType = value; // G, L, W
                            break;

                        case "GDATA":
                            packet.GpsData = ParseGpsData(value);
                            break;

                        case "STATUS":
                            packet.Status = ParseStatus(value);
                            break;

                        case "ALERT":
                            packet.Alert = ParseAlert(value);
                            break;

                        case "CELL":
                            packet.CellInfo = ParseCellInfo(value);
                            break;

                        case "WAY":
                            if (int.TryParse(value, out var way))
                                packet.PositioningWay = way;
                            break;
                    }
                }

                return packet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parseando paquete LOCA: {Content}", content);
                return null;
            }
        }

        /// <summary>
        /// Parsea datos GPS del campo GDATA.
        /// Formato: A,12,160412154800,22.564025,113.242329,5.5,152,900
        /// </summary>
        private S168GpsData? ParseGpsData(string gdataValue)
        {
            try
            {
                var parts = gdataValue.Split(',');
                if (parts.Length < 5) return null;

                var gps = new S168GpsData
                {
                    IsPositioned = parts[0].Trim().Equals("A", StringComparison.OrdinalIgnoreCase)
                };

                if (int.TryParse(parts[1].Trim(), out var sats))
                    gps.Satellites = sats;

                // Parsear timestamp GPS: yyMMddHHmmss
                var timeStr = parts[2].Trim();
                if (timeStr.Length >= 12)
                {
                    if (DateTime.TryParseExact(timeStr[..12], "yyMMddHHmmss",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var gpsTime))
                    {
                        gps.GpsTime = DateTime.SpecifyKind(gpsTime, DateTimeKind.Utc);
                    }
                    else
                    {
                        gps.GpsTime = DateTime.UtcNow;
                    }
                }
                else
                {
                    gps.GpsTime = DateTime.UtcNow;
                }

                if (double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                    gps.Latitude = lat;

                if (double.TryParse(parts[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                    gps.Longitude = lon;

                if (parts.Length > 5 && double.TryParse(parts[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                    gps.Speed = speed;

                if (parts.Length > 6 && double.TryParse(parts[6].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var heading))
                    gps.Heading = heading;

                if (parts.Length > 7 && double.TryParse(parts[7].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var alt))
                    gps.Altitude = alt;

                return gps;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parseando GDATA: {Value}", gdataValue);
                return null;
            }
        }

        /// <summary>
        /// Parsea el campo STATUS.
        /// Formato: battery,signal
        /// </summary>
        private S168Status? ParseStatus(string statusValue)
        {
            try
            {
                var parts = statusValue.Split(',');
                var status = new S168Status();

                if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out var battery))
                    status.Battery = battery;

                if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var signal))
                    status.SignalStrength = signal;

                return status;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parsea flags de alarma del campo ALERT.
        /// Formato: 4 hex chars (ej: "0000", "0010")
        /// </summary>
        private S168Alert? ParseAlert(string alertValue)
        {
            try
            {
                if (ushort.TryParse(alertValue.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flags))
                {
                    return new S168Alert { RawFlags = flags };
                }
                return new S168Alert { RawFlags = 0 };
            }
            catch
            {
                return new S168Alert { RawFlags = 0 };
            }
        }

        /// <summary>
        /// Parsea informacion de estacion base celular.
        /// Formato: count,mcc,mnc,lac,cellid,signal
        /// </summary>
        private S168CellInfo? ParseCellInfo(string cellValue)
        {
            try
            {
                var parts = cellValue.Split(',');
                if (parts.Length < 6) return null;

                return new S168CellInfo
                {
                    CellCount = int.TryParse(parts[0].Trim(), out var count) ? count : 0,
                    Mcc = parts[1].Trim(),
                    Mnc = parts[2].Trim(),
                    Lac = parts[3].Trim(),
                    CellId = parts[4].Trim(),
                    SignalStrength = int.TryParse(parts[5].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var sig) ? sig : 0
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parsea un paquete SYNC (heartbeat).
        /// Formato: SYNC:002b;STATUS:0001;CLOSED:1
        /// </summary>
        public S168SyncPacket? ParseSyncPacket(string content)
        {
            try
            {
                var packet = new S168SyncPacket();
                var sections = content.Split(';');

                foreach (var section in sections)
                {
                    var colonIndex = section.IndexOf(':');
                    if (colonIndex < 0) continue;

                    var keyword = section[..colonIndex].Trim().ToUpperInvariant();
                    var value = section[(colonIndex + 1)..].Trim();

                    switch (keyword)
                    {
                        case "SYNC":
                            packet.HeartbeatNumber = value;
                            break;
                        case "STATUS":
                            packet.Status = ParseStatus(value);
                            break;
                        case "CLOSED":
                            packet.WillClose = value == "1";
                            break;
                    }
                }

                return packet;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parseando paquete SYNC: {Content}", content);
                return null;
            }
        }

        /// <summary>
        /// Construye una respuesta ACK segun el protocolo S168.
        /// Formato: ID#IMEI#SN#length#ACK^KEYWORD,params$
        /// </summary>
        public string BuildAckResponse(string id, string imei, string sn, string keyword, string? extraParams = null)
        {
            var ackContent = $"ACK^{keyword}";
            if (!string.IsNullOrEmpty(extraParams))
                ackContent += $",{extraParams}";

            var contentLength = Encoding.ASCII.GetByteCount(ackContent);
            var lengthHex = contentLength.ToString("x4");

            return $"{id}#{imei}#{sn}#{lengthHex}#{ackContent}$";
        }

        /// <summary>
        /// Construye la respuesta ACK para un paquete SYNC con UTC time.
        /// </summary>
        public string BuildSyncAck(string id, string imei, string sn)
        {
            var utcTime = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            return BuildAckResponse(id, imei, sn, "SYNC", utcTime);
        }

        /// <summary>
        /// Construye la respuesta ACK para un paquete LOCA.
        /// </summary>
        public string BuildLocaAck(string id, string imei, string sn)
        {
            return BuildAckResponse(id, imei, sn, "LOCA");
        }
    }
}
