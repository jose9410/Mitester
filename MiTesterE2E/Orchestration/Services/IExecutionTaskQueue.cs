using MiTesterE2E.Orchestration.Contracts;

namespace MiTesterE2E.Orchestration.Services;

/// <summary>
/// Contrato de la cola de tareas de ejecución en memoria.
/// Abstrae el System.Threading.Channels.Channel&lt;ExecutionTask&gt; para permitir
/// inyección de dependencias y facilitar pruebas unitarias con mocks.
/// </summary>
public interface IExecutionTaskQueue
{
    /// <summary>
    /// Encola una nueva tarea de ejecución.
    /// Operación no bloqueante (ValueTask) porque el Channel es bounded o unbounded
    /// dependiendo de la configuración.
    /// </summary>
    /// <param name="task">La tarea a encolar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación de escritura.</param>
    ValueTask EnqueueAsync(ExecutionTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Desencola (lee) la siguiente tarea disponible.
    /// Se bloquea asincrónicamente hasta que haya una tarea o se cancele el token.
    /// Usado exclusivamente por ExecutionBackgroundWorker.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación del host (HostApplicationLifetime).</param>
    ValueTask<ExecutionTask> DequeueAsync(CancellationToken cancellationToken = default);
}
