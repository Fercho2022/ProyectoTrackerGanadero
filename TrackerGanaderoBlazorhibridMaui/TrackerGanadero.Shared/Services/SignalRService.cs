using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TrackerGanadero.Shared.Models;

namespace TrackerGanadero.Shared.Services
{
    public class SignalRService : IAsyncDisposable
    {
        private readonly ILogger<SignalRService> _logger;
        private HubConnection? _connection;
        private readonly string _baseUrl;

        public event Action<AnimalLocationDto>? LocationUpdated;
        public event Action<AlertDto>? AlertReceived;
        public event Action<AlertDto>? AlertResolved;
        public event Action<string>? ConnectionStatusChanged;

        public SignalRService(ILogger<SignalRService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _baseUrl = configuration.GetSection("ApiSettings")["BaseUrl"] ?? "https://localhost:7028";
        }

        public async Task StartConnectionAsync()
        {
            if (_connection?.State == HubConnectionState.Connected)
                return;

            try
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl($"{_baseUrl}/tracking-hub")
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                    .Build();

                // Configure event handlers
                // LocationUpdate recibe (int animalId, LocationDto location) desde el server
                _connection.On<int, LocationDto>("LocationUpdate", (animalId, location) =>
                {
                    try
                    {
                        if (location != null)
                        {
                            // Construir AnimalLocationDto con los datos disponibles
                            var animalLocation = new AnimalLocationDto
                            {
                                Id = animalId,
                                CurrentLocation = location
                            };
                            LocationUpdated?.Invoke(animalLocation);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing location update");
                    }
                });

                // NewAlert recibe un AlertDto directamente desde el server
                _connection.On<AlertDto>("NewAlert", (alert) =>
                {
                    try
                    {
                        if (alert != null)
                        {
                            AlertReceived?.Invoke(alert);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing alert");
                    }
                });

                // AlertResolved recibe un AlertDto directamente desde el server
                _connection.On<AlertDto>("AlertResolved", (alert) =>
                {
                    try
                    {
                        if (alert != null)
                        {
                            AlertResolved?.Invoke(alert);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing resolved alert");
                    }
                });

                _connection.Closed += async (error) =>
                {
                    ConnectionStatusChanged?.Invoke("Disconnected");
                    _logger.LogWarning("SignalR connection closed: {Error}", error?.Message);
                    await Task.Delay(5000);
                };

                _connection.Reconnected += (connectionId) =>
                {
                    ConnectionStatusChanged?.Invoke("Connected");
                    _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
                    return Task.CompletedTask;
                };

                _connection.Reconnecting += (error) =>
                {
                    ConnectionStatusChanged?.Invoke("Reconnecting");
                    _logger.LogWarning("SignalR reconnecting: {Error}", error?.Message);
                    return Task.CompletedTask;
                };

                await _connection.StartAsync();
                ConnectionStatusChanged?.Invoke("Connected");
                _logger.LogInformation("SignalR connection started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting SignalR connection");
                ConnectionStatusChanged?.Invoke("Error");
                throw;
            }
        }

        public async Task JoinFarmGroupAsync(int farmId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("JoinFarmGroup", farmId.ToString());
                    _logger.LogInformation("Joined farm group: {FarmId}", farmId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error joining farm group {FarmId}", farmId);
                }
            }
        }

        public async Task LeaveFarmGroupAsync(int farmId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("LeaveFarmGroup", farmId.ToString());
                    _logger.LogInformation("Left farm group: {FarmId}", farmId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error leaving farm group {FarmId}", farmId);
                }
            }
        }

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }

    public class AnimalLocationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Tag { get; set; }
        public LocationDto? CurrentLocation { get; set; }
        public int FarmId { get; set; }
        public int? TrackerId { get; set; }
        public string Status { get; set; } = "Healthy";
        public string Gender { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        // Properties for compatibility with the UI
        public int AnimalId => Id;
        public string AnimalName => Name;
        public LocationDto? Location => CurrentLocation;
    }
}