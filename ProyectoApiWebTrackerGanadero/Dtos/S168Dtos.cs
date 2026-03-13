namespace ApiWebTrackerGanado.Dtos
{
    /// <summary>
    /// Trama completa parseada del protocolo S168 Rayoid.
    /// Formato: ID#IMEI#SN#Length#Content$
    /// </summary>
    public class S168Frame
    {
        public string Id { get; set; } = string.Empty;       // ej: "S168"
        public string Imei { get; set; } = string.Empty;     // 15 digitos
        public string Sn { get; set; } = string.Empty;       // 4 hex chars (sequence number)
        public int ContentLength { get; set; }
        public string Content { get; set; } = string.Empty;  // contenido efectivo
        public string PacketType { get; set; } = string.Empty; // LOCA, SYNC, INFO, etc.
    }

    /// <summary>
    /// Datos GPS extraidos del campo GDATA del paquete LOCA.
    /// </summary>
    public class S168GpsData
    {
        public bool IsPositioned { get; set; }   // A=posicionado, V=no posicionado
        public int Satellites { get; set; }
        public DateTime GpsTime { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }        // km/h
        public double Heading { get; set; }      // grados
        public double Altitude { get; set; }     // metros
    }

    /// <summary>
    /// Estado del dispositivo (campo STATUS).
    /// </summary>
    public class S168Status
    {
        public int Battery { get; set; }         // porcentaje 0-100
        public int SignalStrength { get; set; }  // 0-100
    }

    /// <summary>
    /// Flags de alarma (campo ALERT, 2 bytes hex).
    /// </summary>
    public class S168Alert
    {
        public ushort RawFlags { get; set; }
        public bool LowBattery => (RawFlags & 0x0001) != 0;       // Bit0
        public bool Sos => (RawFlags & 0x0002) != 0;              // Bit1
        public bool Vibration => (RawFlags & 0x0004) != 0;        // Bit2
        public bool FenceEntry => (RawFlags & 0x0008) != 0;       // Bit3
        public bool FenceOut => (RawFlags & 0x0010) != 0;         // Bit4
        public bool Tamper => (RawFlags & 0x0020) != 0;           // Bit5 (detach/remove/power off)
        public bool Armed => (RawFlags & 0x0040) != 0;            // Bit6
        public bool Charging => (RawFlags & 0x0080) != 0;         // Bit7
    }

    /// <summary>
    /// Informacion de estacion base celular (campo CELL).
    /// </summary>
    public class S168CellInfo
    {
        public int CellCount { get; set; }
        public string Mcc { get; set; } = string.Empty;
        public string Mnc { get; set; } = string.Empty;
        public string Lac { get; set; } = string.Empty;
        public string CellId { get; set; } = string.Empty;
        public int SignalStrength { get; set; }
    }

    /// <summary>
    /// Paquete LOCA completo parseado.
    /// </summary>
    public class S168LocaPacket
    {
        public string LocationType { get; set; } = "G"; // G=GPS, L=LBS, W=WIFI
        public S168GpsData? GpsData { get; set; }
        public S168Status? Status { get; set; }
        public S168Alert? Alert { get; set; }
        public S168CellInfo? CellInfo { get; set; }
        public int PositioningWay { get; set; } // 0=normal, 1=trigger, 2=alarm, 3=now, 4=tracking
    }

    /// <summary>
    /// Paquete SYNC (heartbeat) parseado.
    /// </summary>
    public class S168SyncPacket
    {
        public string HeartbeatNumber { get; set; } = string.Empty;  // hex
        public S168Status? Status { get; set; }
        public bool WillClose { get; set; }  // CLOSED:1 = va a cerrar la conexion
    }

    /// <summary>
    /// Configuracion del TCP Listener desde appsettings.json.
    /// </summary>
    public class TcpTrackerSettings
    {
        public int Port { get; set; } = 6800;
        public bool Enabled { get; set; } = false;
        public int MaxConnections { get; set; } = 200;
    }
}
