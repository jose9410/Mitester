using System.Threading.Channels;
using MiTesterE2E.Orchestration.Contracts;

namespace MiTesterE2E.Orchestration.Services;

/// <summary>
/// Implementación concreta de IExecutionTaskQueue usando System.Threading.Channels.
/// 
/// Se registra como Singleton en el contenedor DI para que el Channel persista
/// durante toda la vida del proceso y sea compartido entre el Controller (productor)
/// y el BackgroundWorker (consumidor).
/// 
/// Usa un Channel&lt;ExecutionTask&gt; sin límite de capacidad (unbounded) para no bloquear
/// peticiones HTTP de alta densidad. El backpressure se gestiona a nivel de
/// infraestructura (Azure Container Apps escalado horizontal en fases posteriores).
/// </summary>
public sealed class ExecutionTaskQueue : IExecutionTaskQueue
{
    private readonly Channel<ExecutionTask> _channel;
    private readonly ILogger<ExecutionTaskQueue> _logger;

    public ExecutionTaskQueue(
        IConfiguration configuration,
        ILogger<ExecutionTaskQueue> logger)
    {
        _logger = logger;

        // Leemos la capacidad desde appsettings.json
        // 0 o negativo = unbounded (sin límite de backlog)
        var capacity = configuration.GetValue<int>("Orchestration:ChannelCapacity");

        _channel = capacity > 0
            ? Channel.CreateBounded<ExecutionTask>(new BoundedChannelOptions(capacity)
            {
                // Si el Channel está lleno, el productor espera en lugar de soltar tareas
                FullMode = BoundedChannelFullMode.Wait,
                // SingleReader = true optimiza el Channel para un único consumidor (BackgroundWorker)
                SingleReader = true,
                // SingleWriter = false porque múltiples peticiones HTTP concurrentes pueden encolar
                SingleWriter = false
            })
            : Channel.CreateUnbounded<ExecutionTask>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _logger.LogInformation(
            "[ExecutionTaskQueue] Inicializada. Modo: {Mode} | Capacidad: {Cap}",
            capacity > 0 ? "Bounded" : "Unbounded",
            capacity > 0 ? capacity.ToString() : "∞");
    }

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(ExecutionTask task, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(task, cancellationToken);

        _logger.LogInformation(
            "[ExecutionTaskQueue] Tarea encolada. ExecutionId: {Id} | EnqueuedAt: {Ts}",
            task.ExecutionId,
            task.EnqueuedAt);
    }

    /// <inheritdoc />
    public async ValueTask<ExecutionTask> DequeueAsync(CancellationToken cancellationToken = default)
    {
        // ReadAsync se bloquea asincrónicamente hasta que haya un item disponible o se cancele
        var task = await _channel.Reader.ReadAsync(cancellationToken);

        _logger.LogInformation(
            "[ExecutionTaskQueue] Tarea desencolada para procesamiento. ExecutionId: {Id}",
            task.ExecutionId);

        return task;
    }
}
