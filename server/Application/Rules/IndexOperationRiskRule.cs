using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Rules;

/// <summary>
/// Flags index operations (create/drop/alter) as potentially expensive. Does
/// not claim exact execution time.
/// </summary>
public sealed class IndexOperationRiskRule : IMigrationRiskRule
{
    public string RuleId => "INDEX_OPERATION";

    public async Task<IReadOnlyList<RiskFinding>> EvaluateAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<RiskFinding>();

        foreach (var op in context.Migration.Operations.OfType<IndexOperation>())
        {
            var checkedDb = false;
            var found = false;
            long? rowCount = null;

            if (context.DatabaseAvailable && op.TableName is not null)
            {
                checkedDb = true;
                found = await context.Database.TableExistsAsync(op.TableName, cancellationToken);
                if (found)
                {
                    rowCount = await context.Database.GetRowCountAsync(op.TableName, cancellationToken);
                }
            }

            // Larger tables make the operation more expensive.
            var severity = rowCount is > 100_000 ? RiskSeverity.Medium : RiskSeverity.Low;
            var indexLabel = op.IndexName is not null ? $"'{op.IndexName}'" : "index";
            var tableLabel = op.TableName is not null ? $" on '{op.TableName}'" : string.Empty;

            var evidence = found && rowCount is not null
                ? $"Target table '{op.TableName}' holds {rowCount:N0} row(s)."
                : op.RawStatement;

            findings.Add(new RiskFinding
            {
                RuleId = RuleId,
                Title = $"{op.Kind.ToString().ToUpperInvariant()} INDEX {indexLabel}{tableLabel}",
                Severity = severity,
                Category = RiskCategory.Performance,
                Description = $"The migration performs a {op.Kind.ToString().ToLowerInvariant()} index operation, "
                    + "which is potentially expensive on a database object and may hold locks during deployment.",
                AffectedObject = op.TableName ?? op.IndexName,
                Evidence = evidence,
                Recommendation = "Schedule the change during a maintenance window and consider ONLINE operations where supported.",
                Source = checkedDb && found ? AnalysisSource.DatabaseAnalysis : AnalysisSource.StaticAnalysis,
                DatabaseChecked = checkedDb,
                DatabaseObjectFound = found
            });
        }

        return findings;
    }
}
