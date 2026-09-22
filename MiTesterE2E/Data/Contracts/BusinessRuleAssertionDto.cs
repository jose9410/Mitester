using System.Text.Json.Serialization;

namespace MiTesterE2E.Data.Contracts;

/// <summary>
/// DTO para la aserción y evaluación de reglas de negocio en base al porcentaje de consistencia.
/// </summary>
public class BusinessRuleAssertionDto
{
    [JsonPropertyName("expectedValue")]
    public decimal ExpectedValue { get; set; } = 99.50m;

    /// <summary>
    /// Operador de comparación: ">=" | "==" | "<=" | ">" | "<".
    /// </summary>
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = ">=";

    public decimal ActualValue { get; set; }

    public bool Passed { get; set; }

    public string Message { get; set; } = string.Empty;

    public static BusinessRuleAssertionDto Evaluate(decimal actualValue, decimal expectedValue, string op)
    {
        var cleanOp = op?.Trim() ?? ">=";
        bool passed = cleanOp switch
        {
            ">=" => actualValue >= expectedValue,
            "==" => actualValue == expectedValue,
            "<=" => actualValue <= expectedValue,
            ">"  => actualValue > expectedValue,
            "<"  => actualValue < expectedValue,
            _    => actualValue >= expectedValue
        };

        var message = passed
            ? $"Aserción de calidad superada: {actualValue:N2}% {cleanOp} {expectedValue:N2}%"
            : $"Aserción de calidad NO superada: {actualValue:N2}% no cumple condición {cleanOp} {expectedValue:N2}%";

        return new BusinessRuleAssertionDto
        {
            ExpectedValue = expectedValue,
            Operator = cleanOp,
            ActualValue = actualValue,
            Passed = passed,
            Message = message
        };
    }
}
