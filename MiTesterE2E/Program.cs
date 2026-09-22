using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MiTesterE2E.Automation.Services;
using MiTesterE2E.Data.Services;
using MiTesterE2E.Orchestration.Services;
using MiTesterE2E.Orchestration.Workers;
using MiTesterE2E.Persistence.Context;
using MiTesterE2E.Persistence.Multitenancy;
using MiTesterE2E.Persistence.Seeding;
using MiTesterE2E.Telemetry;
using MiTesterE2E.Telemetry.Observability;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────────
// 0. OBSERVABILIDAD Y TELEMETRÍA DISTRIBUIDA (OpenTelemetry & Azure Monitor)
// ─────────────────────────────────────────────────────────────────────────────
var appInsightsConnString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
    ?? builder.Configuration["ApplicationInsights:ConnectionString"];

var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? builder.Configuration["OpenTelemetry:OtlpEndpoint"];

var otelBuilder = builder.Services.AddOpenTelemetry();

otelBuilder.ConfigureResource(resource => resource
    .AddService(serviceName: AppTelemetry.ServiceName, serviceVersion: AppTelemetry.ServiceVersion));

// Activación condicional de Azure Monitor para Azure Container Apps (ACA)
if (!string.IsNullOrWhiteSpace(appInsightsConnString))
{
    otelBuilder.UseAzureMonitor(options =>
    {
        options.ConnectionString = appInsightsConnString;
    });
}

otelBuilder.WithTracing(tracing =>
{
    tracing
        .AddSource(AppTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .SetSampler(new AlwaysOnSampler()); // Muestreo al 100% para certificación bancaria

    // Fallback a consola y OTLP en desarrollo local si no hay credenciales de Azure
    if (string.IsNullOrWhiteSpace(appInsightsConnString))
    {
        tracing.AddConsoleExporter();
        if (!string.IsNullOrWhiteSpace(otelEndpoint))
        {
            tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
        }
    }
});

otelBuilder.WithMetrics(metrics =>
{
    metrics
        .AddMeter(AppTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation();

    if (string.IsNullOrWhiteSpace(appInsightsConnString))
    {
        metrics.AddConsoleExporter();
        if (!string.IsNullOrWhiteSpace(otelEndpoint))
        {
            metrics.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
        }
    }
});

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    if (string.IsNullOrWhiteSpace(appInsightsConnString))
    {
        logging.AddConsoleExporter();
    }
});

// ─────────────────────────────────────────────────────────────────────────────
// 1. CONTROLADORES Y SWAGGER
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title   = "API de Certificación \"Mi Tester E2E\"",
        Version = "v1.1.0",
        Description = "Contratos REST API para la plataforma de certificación de software bancario " +
                      "impulsada por metadatos. Persistencia Multitenant Nivel 1 (Azure SQL / Global Filters)."
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// 2. PERSISTENCIA MULTITENANT (Nivel 1: Discriminador + Global Query Filters)
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, TenantService>();

var sqlConnectionString = builder.Configuration.GetConnectionString("AzureSql");
if (!string.IsNullOrWhiteSpace(sqlConnectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(sqlConnectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));
}
else
{
    // Fallback en memoria para desarrollo ágil / pruebas locales sin Azure SQL activo
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("MiTesterE2E_Db"));
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. SIGNALR – Telemetría en tiempo real
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─────────────────────────────────────────────────────────────────────────────
// 4. AUTOMATIZACIÓN, CONECTORES SQL Y ORQUESTACIÓN
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<ISqlCommandExecutor, SqlCommandExecutor>();
builder.Services.AddSingleton<IPlaywrightCommandExecutor, PlaywrightCommandExecutor>();
builder.Services.AddSingleton<IExecutionTaskQueue, ExecutionTaskQueue>();
builder.Services.AddHostedService<ExecutionBackgroundWorker>();

// ─────────────────────────────────────────────────────────────────────────────
// 5. CORS – Permitir peticiones desde el frontend Angular
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",  // Angular dev server
                "https://app.mitestere2e.banco.internal"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Requerido por SignalR WebSockets
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// 6. CONSTRUCCIÓN DEL PIPELINE
// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Sembrado de datos iniciales multitenant para pruebas bancarias
await DbInitializer.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mi Tester E2E v1.1.0");
        options.RoutePrefix = "swagger"; // Swagger disponible en /swagger
    });
}

app.UseHttpsRedirection();
app.UseCors("AngularFrontend");

// Servir la SPA de Angular (artefactos compilados en wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

// Rutas de controladores REST bajo el prefijo /api
app.MapControllers();

// Ruta del Hub de SignalR (el frontend Angular se conectará a esta URL)
app.MapHub<TelemetryHub>("/hubs/telemetry");

// Fallback para el enrutamiento del lado del cliente de Angular (SPA)
app.MapFallbackToFile("index.html");

app.Run();
