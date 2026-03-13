using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ApiWebTrackerGanado.Services.BackgroundServices
{
    /// <summary>
    /// BackgroundService que escucha conexiones TCP de trackers GPS reales
    /// que usan el protocolo S168 de Shenzhen Rayoid Technology.
    ///
    /// Configuracion en appsettings.json seccion "TcpTracker":
    ///   Port: puerto TCP (default 6800)
    ///   Enabled: true/false (default false)
    ///   MaxConnections: maximo de conexiones simultaneas (default 200)
    /// </summary>
    public class TcpTrackerListenerService : BackgroundService
    {
        private readonly ILogger<TcpTrackerListenerService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly TcpTrackerSettings _settings;
        private readonly ConcurrentDictionary<string, DateTime> _activeConnections = new();
        private TcpListener? _listener;

        public TcpTrackerListenerService(
            ILogger<TcpTrackerListenerService> logger,
            IServiceScopeFactory serviceScopeFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _settings = new TcpTrackerSettings();
            configuration.GetSection("TcpTracker").Bind(_settings);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.Enabled)
            {
                _logger.LogInformation("TCP Tracker Listener DESHABILITADO. Para activarlo, configure TcpTracker:Enabled=true en appsettings.json");
                return;
            }

            _logger.LogInformation("Iniciando TCP Tracker Listener en puerto {Port} (max {MaxConn} conexiones)",
                _settings.Port, _settings.MaxConnections);

            _listener = new TcpListener(IPAddress.Any, _settings.Port);

            try
            {
                _listener.Start();
                _logger.LogInformation("TCP Tracker Listener escuchando en puerto {Port}", _settings.Port);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                        var remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

                        if (_activeConnections.Count >= _settings.MaxConnections)
                        {
                            _logger.LogWarning("Conexion rechazada de {Remote}: limite de {Max} conexiones alcanzado",
                                remoteEp, _settings.MaxConnections);
                            client.Close();
                            continue;
                        }

                        _logger.LogDebug("Nueva conexion TCP desde {Remote}", remoteEp);

                        // Manejar la conexion en un task separado
                        _ = Task.Run(() => HandleClientAsync(client, remoteEp, stoppingToken), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error aceptando conexion TCP");
                        await Task.Delay(1000, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown normal
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fatal en TCP Tracker Listener");
            }
            finally
            {
                _listener?.Stop();
                _logger.LogInformation("TCP Tracker Listener detenido");
            }
        }

        /// <summary>
        /// Maneja una conexion TCP individual de un tracker.
        /// Lee datos del stream, acumula buffer hasta encontrar '$', y procesa cada trama.
        /// </summary>
        private async Task HandleClientAsync(TcpClient client, string remoteEndpoint, CancellationToken stoppingToken)
        {
            string connectionImei = "unknown";

            try
            {
                client.ReceiveTimeout = 600000; // 10 minutos
                client.SendTimeout = 30000;     // 30 segundos

                using var stream = client.GetStream();
                var buffer = new byte[4096];
                var messageBuffer = new StringBuilder();

                while (!stoppingToken.IsCancellationRequested && client.Connected)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        // Conexion cerrada por el tracker
                        break;
                    }

                    if (bytesRead == 0) break; // Conexion cerrada

                    var data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    messageBuffer.Append(data);

                    // Procesar todas las tramas completas (terminan en '$')
                    var fullMessage = messageBuffer.ToString();
                    int dollarIndex;

                    while ((dollarIndex = fullMessage.IndexOf('$')) >= 0)
                    {
                        var frame = fullMessage[..dollarIndex];
                        fullMessage = fullMessage[(dollarIndex + 1)..];

                        if (!string.IsNullOrWhiteSpace(frame))
                        {
                            var response = await ProcessFrameAsync(frame.Trim(), remoteEndpoint);
                            connectionImei = ExtractImei(frame) ?? connectionImei;

                            // Registrar conexion activa
                            if (connectionImei != "unknown")
                            {
                                _activeConnections[connectionImei] = DateTime.UtcNow;
                            }

                            // Enviar respuesta ACK al tracker
                            if (!string.IsNullOrEmpty(response))
                            {
                                var responseBytes = Encoding.ASCII.GetBytes(response);
                                try
                                {
                                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length, stoppingToken);
                                    await stream.FlushAsync(stoppingToken);
                                }
                                catch (IOException)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    messageBuffer.Clear();
                    if (fullMessage.Length > 0)
                        messageBuffer.Append(fullMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en conexion TCP con {Imei} ({Remote})", connectionImei, remoteEndpoint);
            }
            finally
            {
                if (connectionImei != "unknown")
                {
                    _activeConnections.TryRemove(connectionImei, out _);
                }

                try { client.Close(); } catch { }
                _logger.LogDebug("Conexion cerrada: {Imei} ({Remote})", connectionImei, remoteEndpoint);
            }
        }

        /// <summary>
        /// Extrae el IMEI de una trama cruda.
        /// </summary>
        private string? ExtractImei(string rawFrame)
        {
            var parts = rawFrame.Split('#');
            return parts.Length >= 2 && parts[1].Length == 15 ? parts[1] : null;
        }

        /// <summary>
        /// Procesa una trama completa y retorna la respuesta ACK correspondiente.
        /// </summary>
        private async Task<string?> ProcessFrameAsync(string rawFrame, string remoteEndpoint)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var parser = scope.ServiceProvider.GetRequiredService<S168ProtocolParser>();

            var frame = parser.ParseFrame(rawFrame);
            if (frame == null)
            {
                _logger.LogWarning("Trama no parseable de {Remote}: {Raw}", remoteEndpoint, rawFrame[..Math.Min(100, rawFrame.Length)]);
                return null;
            }

            _logger.LogDebug("Trama {Type} de IMEI {Imei} (SN:{Sn})", frame.PacketType, frame.Imei, frame.Sn);

            switch (frame.PacketType)
            {
                case "LOCA":
                    return await HandleLocaPacketAsync(frame, parser, scope);

                case "SYNC":
                    return HandleSyncPacket(frame, parser, scope);

                case "INFO":
                    HandleInfoPacket(frame);
                    return null; // INFO no requiere respuesta

                default:
                    _logger.LogDebug("Tipo de paquete no manejado: {Type}", frame.PacketType);
                    return null;
            }
        }

        /// <summary>
        /// Procesa un paquete LOCA: parsea GPS, mapea a TrackerDataDto, y lo envia al TrackingService.
        /// </summary>
        private async Task<string?> HandleLocaPacketAsync(S168Frame frame, S168ProtocolParser parser, IServiceScope scope)
        {
            var loca = parser.ParseLocaPacket(frame.Content);
            if (loca?.GpsData == null || !loca.GpsData.IsPositioned)
            {
                _logger.LogDebug("LOCA sin posicion GPS de IMEI {Imei}", frame.Imei);
                return parser.BuildLocaAck(frame.Id, frame.Imei, frame.Sn);
            }

            // Mapear a TrackerDataDto existente para reutilizar toda la infraestructura
            var trackerData = new TrackerDataDto
            {
                DeviceId = frame.Imei,
                Latitude = loca.GpsData.Latitude,
                Longitude = loca.GpsData.Longitude,
                Altitude = loca.GpsData.Altitude,
                Speed = loca.GpsData.Speed,
                ActivityLevel = EstimateActivityLevel(loca.GpsData.Speed),
                Temperature = 0, // S168 no envia temperatura interna
                BatteryLevel = loca.Status?.Battery ?? 0,
                SignalStrength = loca.Status?.SignalStrength ?? 0,
                Timestamp = loca.GpsData.GpsTime,
                LocationType = loca.LocationType switch
                {
                    "L" => "LBS",
                    "W" => "WIFI",
                    _ => "GPS"
                }
            };

            try
            {
                var trackingService = scope.ServiceProvider.GetRequiredService<ITrackingService>();
                await trackingService.ProcessTrackerDataAsync(trackerData);

                _logger.LogDebug("LOCA procesado: IMEI {Imei} -> ({Lat:F6}, {Lon:F6}) {Speed:F1} km/h, Bat:{Bat}%",
                    frame.Imei, trackerData.Latitude, trackerData.Longitude,
                    trackerData.Speed, trackerData.BatteryLevel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando LOCA de IMEI {Imei}", frame.Imei);
            }

            return parser.BuildLocaAck(frame.Id, frame.Imei, frame.Sn);
        }

        /// <summary>
        /// Procesa un paquete SYNC (heartbeat): actualiza LastSeen del tracker.
        /// </summary>
        private string HandleSyncPacket(S168Frame frame, S168ProtocolParser parser, IServiceScope scope)
        {
            var sync = parser.ParseSyncPacket(frame.Content);

            _logger.LogDebug("SYNC de IMEI {Imei}: heartbeat #{Num}, Bat:{Bat}%, WillClose:{Close}",
                frame.Imei,
                sync?.HeartbeatNumber ?? "?",
                sync?.Status?.Battery ?? 0,
                sync?.WillClose ?? false);

            // Heartbeat = el tracker esta vivo, actualizar LastSeen via un TrackerDataDto minimo
            // Solo si tiene status con bateria (para no crear location history vacio)
            if (sync?.Status != null)
            {
                try
                {
                    using var innerScope = _serviceScopeFactory.CreateScope();
                    var context = innerScope.ServiceProvider.GetRequiredService<ApiWebTrackerGanado.Data.CattleTrackingContext>();
                    var tracker = context.Trackers.FirstOrDefault(t => t.DeviceId == frame.Imei);
                    if (tracker != null)
                    {
                        tracker.LastSeen = DateTime.UtcNow;
                        tracker.IsOnline = true;
                        tracker.BatteryLevel = sync.Status.Battery;
                        context.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error actualizando tracker en SYNC: {Imei}", frame.Imei);
                }
            }

            return parser.BuildSyncAck(frame.Id, frame.Imei, frame.Sn);
        }

        /// <summary>
        /// Procesa un paquete INFO: loguea la informacion del dispositivo.
        /// </summary>
        private void HandleInfoPacket(S168Frame frame)
        {
            _logger.LogInformation("INFO de IMEI {Imei}: {Content}", frame.Imei, frame.Content);
        }

        /// <summary>
        /// Estima el nivel de actividad (1-10) basado en la velocidad.
        /// </summary>
        private static int EstimateActivityLevel(double speedKmh)
        {
            return speedKmh switch
            {
                < 0.5 => 1,    // Descansando
                < 1.0 => 2,    // Casi inmovil
                < 2.0 => 3,    // Pastando lento
                < 3.0 => 4,    // Pastando
                < 5.0 => 5,    // Caminando
                < 8.0 => 6,    // Caminando rapido
                < 12.0 => 7,   // Trotando
                < 20.0 => 8,   // Corriendo
                < 30.0 => 9,   // Velocidad alta
                _ => 10        // Velocidad anormal (posible vehiculo)
            };
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _listener?.Stop();
            _activeConnections.Clear();
            await base.StopAsync(cancellationToken);
        }
    }
}
