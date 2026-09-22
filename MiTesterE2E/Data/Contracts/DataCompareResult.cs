namespace MiTesterE2E.Data.Contracts;

/// <summary>
/// Resultado consolidado de una operación de comparación y conciliación masiva de datos.
/// </summary>
public class DataCompareResult
{
    public long TotalRecordsProcessed { get; set; }

    public long MatchingRecords { get; set; }

    public int DiscrepanciesCount { get; set; }

    public decimal ConsistencyPercentage { get; set; }

    public decimal TotalMonetaryDiscrepancy { get; set; }

    public double DurationMs { get; set; }

    public bool IsSuccess => DiscrepanciesCount == 0 && ConsistencyPercentage >= 99.50m;

    public List<DiscrepancyItem> SampleDiscrepancies { get; set; } = new();
}

public class DiscrepancyItem
{
    public string InconsistencyId { get; set; } = string.Empty;
    public string RecordKey { get; set; } = string.Empty;
    public string FieldAffected { get; set; } = string.Empty;
    public decimal MonetaryImpact { get; set; }
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string SuggestedTag { get; set; } = "DISCREPANCIA_VALOR";
    public string Description { get; set; } = string.Empty;
}
