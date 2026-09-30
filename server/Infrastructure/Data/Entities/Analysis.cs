using SchemaSentinel.Domain.Enums;

namespace SchemaSentinel.Infrastructure.Data.Entities;

/// <summary>Persisted summary of a single analysis run.</summary>
public sealed class Analysis
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public string ScriptText { get; set; } = string.Empty;
    public RiskSeverity OverallSeverity { get; set; }
    public string Summary { get; set; } = string.Empty;

    public List<AnalysisFinding> Findings { get; set; } = [];
}
