using SchemaSentinel.Application.Interfaces;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Rules;

/// <summary>Everything a rule needs to evaluate a migration.</summary>
public sealed class RuleEvaluationContext
{
    public required ParsedMigration Migration { get; init; }
    public required IDatabaseMetadataAnalyzer Database { get; init; }

    /// <summary>True when the target database was reachable for this analysis.</summary>
    public required bool DatabaseAvailable { get; init; }
}

/// <summary>
/// A single, independently testable risk rule. Rules inspect the parsed
/// migration (and optionally read-only database metadata) and emit findings.
/// </summary>
public interface IMigrationRiskRule
{
    string RuleId { get; }

    Task<IReadOnlyList<RiskFinding>> EvaluateAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken);
}
