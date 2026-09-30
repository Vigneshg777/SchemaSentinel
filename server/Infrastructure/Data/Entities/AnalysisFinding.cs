using SchemaSentinel.Domain.Enums;

namespace SchemaSentinel.Infrastructure.Data.Entities;

/// <summary>Persisted individual finding belonging to an <see cref="Analysis"/>.</summary>
public sealed class AnalysisFinding
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public RiskSeverity Severity { get; set; }
    public RiskCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AffectedObject { get; set; }
    public string? Evidence { get; set; }
    public string? Recommendation { get; set; }
    public AnalysisSource Source { get; set; }

    public Analysis? Analysis { get; set; }
}
