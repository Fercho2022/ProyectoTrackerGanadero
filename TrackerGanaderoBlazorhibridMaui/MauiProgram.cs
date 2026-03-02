using Microsoft.AspNetCore.Components.WebView.Maui;
using TrackerGanaderoBlazorHibridMaui.Services;
using TrackerGanadero.Shared.Services;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace TrackerGanaderoBlazorHibridMaui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("FontAwesome.otf", "FontAwesome");
			});

		builder.Services.AddMauiBlazorWebView();
#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

		// Configuration
		var assembly = Assembly.GetExecutingAssembly();
		using var stream = assembly.GetManifestResourceStream("TrackerGanaderoBlazorHibridMaui.appsettings.json");

		if (stream != null)
		{
			var config = new ConfigurationBuilder()
				.AddJsonStream(stream)
				.Build();
			builder.Configuration.AddConfiguration(config);
		}

		// Platform-specific interface implementations (MAUI)
		builder.Services.AddSingleton<ITokenStorageService, MauiTokenStorageService>();
		builder.Services.AddSingleton<IGeolocationService, MauiGeolocationService>();
		builder.Services.AddSingleton<ITextToSpeech>(TextToSpeech.Default);
		builder.Services.AddSingleton<ITextToSpeechService, MauiTextToSpeechService>();

		// HttpClient Configuration
		var httpClientConfiguration = (HttpClient client) =>
		{
			try
			{
				var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];
				client.BaseAddress = new Uri(baseUrl);
				client.DefaultRequestHeaders.Add("Accept", "application/json");
				client.Timeout = TimeSpan.FromSeconds(30);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error configuring HttpClient: {ex.Message}");
			}
		};

		var httpMessageHandlerConfiguration = () =>
		{
			try
			{
#if WINDOWS
				return new HttpClientHandler()
				{
					ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
				};
#else
				return new HttpClientHandler();
#endif
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error creating HttpClientHandler: {ex.Message}");
				return new HttpClientHandler();
			}
		};

		// HttpClients for different services
		builder.Services.AddTransient<AuthHeaderHandler>();

		builder.Services.AddHttpClient<HttpService>(httpClientConfiguration)
			.ConfigurePrimaryHttpMessageHandler(httpMessageHandlerConfiguration)
			.AddHttpMessageHandler<AuthHeaderHandler>();

		builder.Services.AddHttpClient<LicenseService>(httpClientConfiguration)
			.ConfigurePrimaryHttpMessageHandler(httpMessageHandlerConfiguration)
			.AddHttpMessageHandler<AuthHeaderHandler>();

		builder.Services.AddHttpClient<TrackerManagementService>(httpClientConfiguration)
			.ConfigurePrimaryHttpMessageHandler(httpMessageHandlerConfiguration)
			.AddHttpMessageHandler<AuthHeaderHandler>();

		builder.Services.AddHttpClient<NavigationService>()
			.ConfigurePrimaryHttpMessageHandler(httpMessageHandlerConfiguration);

		// Shared Services
		builder.Services.AddScoped<AuthService>();
		builder.Services.AddSingleton<AnimalService>();
		builder.Services.AddSingleton<HealthService>();
		builder.Services.AddSingleton<SignalRService>();
		builder.Services.AddSingleton<AlertService>();
		builder.Services.AddSingleton<FarmService>();
		builder.Services.AddSingleton<TrackingService>();
		builder.Services.AddSingleton<FarmTrackerService>();
		builder.Services.AddSingleton<SettingsStateService>();
		builder.Services.AddSingleton<VoiceNavigationService>();

		return builder.Build();
	}
}
