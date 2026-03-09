using ApiWebTrackerGanado.Data;
using ApiWebTrackerGanado.Interfaces;
using ApiWebTrackerGanado.Mappings;
using ApiWebTrackerGanado.Services.BackgroundServices;
using ApiWebTrackerGanado.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ApiWebTrackerGanado.Hubs;
using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Models;
using ApiWebTrackerGanado.Repositories;
using ApiWebTrackerGanado.Middleware;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Cattle Tracking API", Version = "v1" });

    // Define the JWT bearer scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(document => new()
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Database Configuration
builder.Services.AddDbContext<CattleTrackingContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        x => x.UseNetTopologySuite())
           .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
           .EnableDetailedErrors(builder.Environment.IsDevelopment());
});

// Repository Pattern - Register repositories with their interfaces
builder.Services.AddScoped<IAnimalRepository, AnimalRepository>();
builder.Services.AddScoped<IFarmRepository, FarmRepository>();
builder.Services.AddScoped<ITrackerRepository, TrackerRepository>();
builder.Services.AddScoped<ILocationHistoryRepository, LocationHistoryRepository>();
builder.Services.AddScoped<IPastureRepository, PastureRepository>();
builder.Services.AddScoped<IPastureUsageRepository, PastureUsageRepository>();
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IHealthRecordRepository, HealthRecordRepository>();
builder.Services.AddScoped<IWeightRecordRepository, WeightRecordRepository>();
builder.Services.AddScoped<IBreedingRecordRepository, BreedingRecordRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Business Services
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<EmailNotificationService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<IPastureService, PastureService>();

// Customer & License Management Services
builder.Services.AddScoped<LicenseService>();
builder.Services.AddScoped<TrackerDiscoveryService>();
builder.Services.AddScoped<FarmTrackerIntegrationService>();

// Background Services - Re-enabled after implementing missing interface methods
builder.Services.AddHostedService<AlertProcessingService>();
builder.Services.AddHostedService<BreedingAnalysisService>();
builder.Services.AddHostedService<GeofencingMonitorService>();
builder.Services.AddHostedService<NoSignalDetectionService>();
builder.Services.AddHostedService<TrackerStatusMonitorService>();

// Authentication - TEMPORALLY DISABLED
// TODO: Re-enable authentication when implemented properly

var jwtSecret = builder.Configuration["JWT:Secret"] ??
    throw new InvalidOperationException("JWT Secret is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        // Allow the token to be sent via SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/tracking-hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Authorization
builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// AutoMapper
builder.Services.AddAutoMapper(cfg => {
    cfg.AddProfile<MappingProfile>();
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policyBuilder =>
    {
        policyBuilder
            .WithOrigins("http://localhost:3000", "http://localhost:5000", "https://localhost:5001", "http://192.168.1.100:5192",
                        "http://localhost:7218", "https://localhost:7218", "http://localhost:5236", "https://localhost:5236",
                        "http://localhost:5280")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Required for SignalR
    });

    options.AddPolicy("Production", policyBuilder =>
    {
        policyBuilder
            // TODO: Replace with actual production frontend URL
            .WithOrigins("http://localhost:3000", "http://localhost:5000", "https://localhost:5001", "http://192.168.1.100:5192")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cattle Tracking API V1");
        c.RoutePrefix = "swagger"; // Swagger UI at /swagger
    });
}

// Custom Middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (!app.Environment.IsDevelopment())
{
    app.UseCors("Production");
}
else
{
    app.UseCors("AllowAll");
}

// TODO: Re-enable authentication when implemented properly
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LiveTrackingHub>("/tracking-hub");
app.MapHealthChecks("/health");

// Test and setup database in development
if (app.Environment.IsDevelopment())
{
    try
    {
        await ApiWebTrackerGanado.TestConnection.TestDatabaseConnection();
        await ApiWebTrackerGanado.TestConnection.CreateTablesDirectly();

        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CattleTrackingContext>();

        // Enable PostGIS extension first
        try
        {
            await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS postgis;");
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("PostGIS extension enabled successfully");
        }
        catch (Exception postgisEx)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Could not enable PostGIS extension: {Message}", postgisEx.Message);
        }

        // Apply pending migrations
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Could not apply database migrations: {Message}", ex.Message);
        logger.LogInformation("Database migrations will need to be applied manually using: dotnet ef database update");
    }
}

app.Run();