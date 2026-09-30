using SchemaSentinel.Domain.Enums;

namespace SchemaSentinel.Domain.Models;

/// <summary>
/// A single explainable risk identified in a migration. Describes what is
/// changing, why it may be risky, the affected object, the supporting evidence
/// and the recommended mitigation.
/// </summary>
public sealed class RiskFinding
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string RuleId { get; init; }
    public required string Title { get; init; }
    public required RiskSeverity Severity { get; init; }
    public required RiskCategory Category { get; init; }
    public required string Description { get; init; }
    public string? AffectedObject { get; init; }
    public string? Evidence { get; init; }
    public string? Recommendation { get; init; }
    public required AnalysisSource Source { get; init; }

    /// <summary>True when the target database was consulted for this finding.</summary>
    public bool DatabaseChecked { get; init; }

    /// <summary>True when the referenced object was found in the target database.</summary>
    public bool DatabaseObjectFound { get; init; }
}
