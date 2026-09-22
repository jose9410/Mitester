namespace MiTesterE2E.Automation.Contracts;

/// <summary>
/// Resultado de la ejecución de una secuencia de comandos de UI Playwright.
/// </summary>
public class UiExecutionResult
{
    public bool Success { get; set; }

    public int TotalCommands { get; set; }

    public int ExecutedCommandsCount { get; set; }

    public string? LastExecutedStep { get; set; }

    public string? ErrorMessage { get; set; }

    public UiCommandDto? FailedCommand { get; set; }

    public double DurationMs { get; set; }

    /// <summary>
    /// Captura en JPEG (75%) Base64 tomada ÚNICAMENTE si ocurre un fallo (ON_FAILURE_ONLY).
    /// </summary>
    public string? ScreenshotBase64 { get; set; }

    /// <summary>
    /// Bandera booleana ligera para SignalR.
    /// </summary>
    public bool HasScreenshot => !string.IsNullOrEmpty(ScreenshotBase64);
}
