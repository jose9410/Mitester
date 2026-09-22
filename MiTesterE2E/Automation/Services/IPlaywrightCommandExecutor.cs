using MiTesterE2E.Automation.Contracts;

namespace MiTesterE2E.Automation.Services;

/// <summary>
/// Contrato del motor de ejecución física de automatización de interfaz de usuario con Microsoft.Playwright.
/// </summary>
public interface IPlaywrightCommandExecutor
{
    /// <summary>
    /// Ejecuta una secuencia de comandos de UI en un navegador Chromium Headless.
    /// Si ocurre un fallo, captura automáticamente una captura de pantalla en JPEG 75% Base64.
    /// </summary>
    /// <param name="executionId">Identificador de la corrida.</param>
    /// <param name="commands">Lista de comandos UI a ejecutar en secuencia.</param>
    /// <param name="onProgressCallback">Callback opcional para reportar progreso comando a comando.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado detallado de la ejecución.</returns>
    Task<UiExecutionResult> ExecuteSequenceAsync(
        string executionId,
        IReadOnlyList<UiCommandDto> commands,
        Func<int, int, string, Task>? onProgressCallback = null,
        CancellationToken cancellationToken = default);
}
