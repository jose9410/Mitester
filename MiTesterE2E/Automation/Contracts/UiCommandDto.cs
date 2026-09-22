using System.Text.Json.Serialization;

namespace MiTesterE2E.Automation.Contracts;

/// <summary>
/// Representa un comando atómico de automatización de interfaz de usuario soportado por el motor Playwright.
/// </summary>
public class UiCommandDto
{
    /// <summary>
    /// Tipo de comando: NAVIGATE, CLICK, FILL, SELECT_OPTION, WAIT_FOR_SELECTOR, ASSERT_TEXT, SCREENSHOT.
    /// </summary>
    [JsonPropertyName("commandType")]
    public string CommandType { get; set; } = string.Empty;

    /// <summary>
    /// URL destino para comandos NAVIGATE.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Selector CSS o XPath para interactuar con el elemento del DOM.
    /// </summary>
    [JsonPropertyName("selector")]
    public string? Selector { get; set; }

    /// <summary>
    /// Valor o texto para comandos FILL o SELECT_OPTION.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// Tiempo de espera específico para este comando en segundos (por defecto 30s).
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Descripción opcional para enriquecer los logs y la telemetría en tiempo real.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
