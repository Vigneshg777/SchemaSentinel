using SchemaSentinel.Domain.Enums;

namespace SchemaSentinel.Domain.Models;

/// <summary>The structured result returned to the client for a single analysis.</summary>
public sealed class AnalysisResult
{
    public required Guid AnalysisId { get; init; }
    public required RiskSeverity OverallSeverity { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<RiskFinding> Findings { get; init; }
    public required IReadOnlyList<AffectedObject> AffectedObjects { get; init; }
    public required DatabaseContext DatabaseContext { get; init; }
    public required IReadOnlyList<string> Recommendations { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? ScriptName { get; init; }
}
