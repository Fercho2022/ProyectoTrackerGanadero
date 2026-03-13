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

// S168 Protocol Parser (para trackers GPS reales via TCP)
builder.Services.AddScoped<S168ProtocolParser>();

// Background Services - Re-enabled after implementing missing interface methods
builder.Services.AddHostedService<AlertProcessingService>();
builder.Services.AddHostedService<BreedingAnalysisService>();
builder.Services.AddHostedService<GeofencingMonitorService>();
builder.Services.AddHostedService<NoSignalDetectionService>();
builder.Services.AddHostedService<TrackerStatusMonitorService>();

// TCP Listener para trackers GPS reales (protocolo S168 Rayoid)
// Configurado en appsettings.json -> TcpTracker (Enabled: false por defecto)
builder.Services.AddHostedService<TcpTrackerListenerService>();

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

        // Auto-asignar categorias: resetear si hay mas de 1 toro por granja (bug fix)
        // y asignar a los que no tienen
        var todosAnimales = await context.Animals.ToListAsync();
        var torosAsignados = new HashSet<int>(); // FarmIds que ya tienen toro
        var cambios = false;

        // Primero: identificar granjas que ya tienen toro correcto (1 solo)
        var torosPorGranja = todosAnimales
            .Where(a => a.Category == "Toro")
            .GroupBy(a => a.FarmId);
        foreach (var grupo in torosPorGranja)
        {
            var toros = grupo.ToList();
            // Mantener solo el primero, los demas pasar a Novillo
            torosAsignados.Add(grupo.Key);
            for (int i = 1; i < toros.Count; i++)
            {
                toros[i].Category = "Novillo";
                cambios = true;
            }
        }

        // Segundo: asignar categorias a los que no tienen
        foreach (var animal in todosAnimales.Where(a => string.IsNullOrEmpty(a.Category)))
        {
            if (animal.Gender.ToLower() == "male")
            {
                if (!torosAsignados.Contains(animal.FarmId))
                {
                    animal.Category = "Toro";
                    torosAsignados.Add(animal.FarmId);
                }
                else
                {
                    animal.Category = "Novillo";
                }
            }
            else
            {
                animal.Category = "Vaca";
            }
            cambios = true;
        }

        if (cambios)
        {
            await context.SaveChangesAsync();
            var logger2 = app.Services.GetRequiredService<ILogger<Program>>();
            logger2.LogInformation("Categorias de animales actualizadas correctamente");
        }

        // Actualizar boundary de todas las granjas con el poligono correcto
        var farms = await context.Farms.ToListAsync();
        var boundaryCoords = new (double Lat, double Lng)[]
        {
            (-33.059810, -60.485645), (-33.044702, -60.483584), (-33.028642, -60.486746), (-33.008779, -60.503404),
            (-32.997118, -60.485372), (-33.001149, -60.476099), (-33.016696, -60.467684), (-33.022021, -60.460986),
            (-33.028930, -60.453087), (-33.042745, -60.443641), (-33.051235, -60.439863), (-33.059148, -60.430761),
            (-33.064039, -60.432135), (-33.074253, -60.433337), (-33.081733, -60.445702), (-33.082883, -60.455319),
            (-33.079863, -60.464249), (-33.068643, -60.477301),
        };

        foreach (var farm in farms)
        {
            // Verificar si el boundary actual es diferente
            var existingBoundaries = await context.FarmBoundaries
                .Where(fb => fb.FarmId == farm.Id)
                .OrderBy(fb => fb.SequenceOrder)
                .ToListAsync();

            bool needsUpdate = existingBoundaries.Count != boundaryCoords.Length;
            if (!needsUpdate)
            {
                for (int i = 0; i < boundaryCoords.Length; i++)
                {
                    if (Math.Abs(existingBoundaries[i].Latitude - boundaryCoords[i].Lat) > 0.000001 ||
                        Math.Abs(existingBoundaries[i].Longitude - boundaryCoords[i].Lng) > 0.000001)
                    {
                        needsUpdate = true;
                        break;
                    }
                }
            }

            if (needsUpdate)
            {
                // Eliminar boundaries existentes
                if (existingBoundaries.Any())
                    context.FarmBoundaries.RemoveRange(existingBoundaries);

                // Agregar nuevos boundaries
                for (int i = 0; i < boundaryCoords.Length; i++)
                {
                    context.FarmBoundaries.Add(new ApiWebTrackerGanado.Models.FarmBoundary
                    {
                        FarmId = farm.Id,
                        Latitude = boundaryCoords[i].Lat,
                        Longitude = boundaryCoords[i].Lng,
                        SequenceOrder = i + 1,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await context.SaveChangesAsync();
                var loggerBoundary = app.Services.GetRequiredService<ILogger<Program>>();
                loggerBoundary.LogInformation("Boundary de granja '{FarmName}' (ID: {FarmId}) actualizado con {Count} coordenadas",
                    farm.Name, farm.Id, boundaryCoords.Length);
            }
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Could not apply database migrations: {Message}", ex.Message);
        logger.LogInformation("Database migrations will need to be applied manually using: dotnet ef database update");
    }
}

app.Run();