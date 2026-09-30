using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Rules;

/// <summary>
/// Detects a nullable-to-NOT NULL change on an existing column. When the
/// database is available it counts existing NULL values that would block the
/// change.
/// </summary>
public sealed class NullabilityChangeRiskRule : IMigrationRiskRule
{
    public string RuleId => "NULLABILITY_CHANGE";

    public async Task<IReadOnlyList<RiskFinding>> EvaluateAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<RiskFinding>();

        foreach (var op in context.Migration.Operations.OfType<AlterColumnOperation>())
        {
            if (op.Nullability != ColumnNullability.NotNull)
            {
                continue;
            }

            var checkedDb = false;
            var found = false;
            long? nullCount = null;
            var currentlyNullable = true;

            if (context.DatabaseAvailable)
            {
                checkedDb = true;
                var column = await context.Database.GetColumnAsync(op.TableName, op.ColumnName, cancellationToken);
                if (column is not null)
                {
                    found = true;
                    currentlyNullable = column.IsNullable;
                    if (currentlyNullable)
                    {
                        nullCount = await context.Database.GetNullCountAsync(op.TableName, op.ColumnName, cancellationToken);
                    }
                }
            }

            // Column is already NOT NULL in the database — no risk from this change.
            if (found && !currentlyNullable)
            {
                continue;
            }

            var hasNulls = nullCount is > 0;
            var severity = hasNulls ? RiskSeverity.High : RiskSeverity.Medium;

            var evidence = found
                ? (nullCount is not null
                    ? $"Existing NULL values: {nullCount:N0}"
                    : $"Column '{op.ColumnName}' is currently nullable.")
                : $"Statement sets '{op.ColumnName}' to NOT NULL.";

            findings.Add(new RiskFinding
            {
                RuleId = RuleId,
                Title = $"NULL to NOT NULL on '{op.TableName}.{op.ColumnName}'",
                Severity = severity,
                Category = RiskCategory.Constraint,
                Description = hasNulls
                    ? $"Enforcing NOT NULL on '{op.ColumnName}' will fail because existing rows contain NULL values."
                    : $"Changing '{op.ColumnName}' to NOT NULL will fail if any row holds a NULL value at deployment time.",
                AffectedObject = $"{op.TableName}.{op.ColumnName}",
                Evidence = evidence,
                Recommendation = "Backfill or default the NULL values first, then apply the NOT NULL constraint.",
                Source = checkedDb && found ? AnalysisSource.DatabaseAnalysis : AnalysisSource.StaticAnalysis,
                DatabaseChecked = checkedDb,
                DatabaseObjectFound = found
            });
        }

        return findings;
    }
}
