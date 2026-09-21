using MiTesterE2E.Orchestration.Services;
using MiTesterE2E.Orchestration.Workers;
using MiTesterE2E.Telemetry;

var builder = WebApplication.CreateBuilder(args);

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
                      "impulsada por metadatos. Fase 1: Orquestación y Telemetría en tiempo real."
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// 2. SIGNALR – Telemetría en tiempo real (no requiere paquete externo en .NET 8)
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─────────────────────────────────────────────────────────────────────────────
// 3. ORQUESTACIÓN: Cola en memoria (Channel) + BackgroundWorker
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IExecutionTaskQueue, ExecutionTaskQueue>();
builder.Services.AddHostedService<ExecutionBackgroundWorker>();

// ─────────────────────────────────────────────────────────────────────────────
// 4. CORS – Permitir peticiones desde el frontend Angular (ajustar en producción)
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
// 5. CONSTRUCCIÓN DE LA APLICACIÓN
// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mi Tester E2E v1.1.0");
        options.RoutePrefix = string.Empty; // Swagger en la raíz "/"
    });
}

app.UseHttpsRedirection();
app.UseCors("AngularFrontend");
app.UseAuthorization();

// Rutas de controladores REST bajo el prefijo /api
app.MapControllers();

// Ruta del Hub de SignalR (el frontend Angular se conectará a esta URL)
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();
