using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TrackerGanadero.Web;
using TrackerGanadero.Web.Services;
using TrackerGanadero.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API Base URL - configurable
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5192";

// Platform-specific interface implementations (Web/Browser)
builder.Services.AddScoped<ITokenStorageService, WebTokenStorageService>();
builder.Services.AddScoped<IGeolocationService, WebGeolocationService>();
builder.Services.AddScoped<ITextToSpeechService, WebTextToSpeechService>();

// AuthHeaderHandler
builder.Services.AddScoped<AuthHeaderHandler>();

// HttpClients
builder.Services.AddHttpClient<HttpService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddHttpClient<LicenseService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddHttpClient<TrackerManagementService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddHttpClient<NavigationService>();

// Shared Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AnimalService>();
builder.Services.AddScoped<HealthService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<FarmService>();
builder.Services.AddScoped<TrackingService>();
builder.Services.AddScoped<FarmTrackerService>();
builder.Services.AddScoped<SettingsStateService>();
builder.Services.AddScoped<VoiceNavigationService>();
builder.Services.AddScoped<NotificationSettingsService>();

await builder.Build().RunAsync();
