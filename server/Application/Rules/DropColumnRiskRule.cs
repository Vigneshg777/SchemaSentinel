using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Rules;

/// <summary>Flags DROP COLUMN operations as destructive with potential data loss.</summary>
public sealed class DropColumnRiskRule : IMigrationRiskRule
{
    public string RuleId => "DROP_COLUMN";

    public async Task<IReadOnlyList<RiskFinding>> EvaluateAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<RiskFinding>();

        foreach (var op in context.Migration.Operations.OfType<DropColumnOperation>())
        {
            var checkedDb = false;
            var found = false;
            long? rowCount = null;

            if (context.DatabaseAvailable)
            {
                checkedDb = true;
                var column = await context.Database.GetColumnAsync(op.TableName, op.ColumnName, cancellationToken);
                found = column is not null;
                if (found)
                {
                    rowCount = await context.Database.GetRowCountAsync(op.TableName, cancellationToken);
                }
            }

            var evidence = found
                ? $"Column '{op.ColumnName}' exists on '{op.TableName}'"
                    + (rowCount is not null ? $" which holds {rowCount:N0} row(s)." : ".")
                : $"Statement drops column '{op.ColumnName}' from '{op.TableName}'.";

            findings.Add(new RiskFinding
            {
                RuleId = RuleId,
                Title = $"DROP COLUMN '{op.ColumnName}' on '{op.TableName}'",
                Severity = RiskSeverity.High,
                Category = RiskCategory.DataLoss,
                Description = $"The migration removes column '{op.ColumnName}' from '{op.TableName}'. "
                    + "Any data stored in this column will be permanently lost and dependent views, indexes or constraints may break.",
                AffectedObject = $"{op.TableName}.{op.ColumnName}",
                Evidence = evidence,
                Recommendation = "Verify the column is unused by applications, views, indexes and constraints. Archive its data if it may be needed before dropping.",
                Source = checkedDb && found ? AnalysisSource.DatabaseAnalysis : AnalysisSource.StaticAnalysis,
                DatabaseChecked = checkedDb,
                DatabaseObjectFound = found
            });
        }

        return findings;
    }
}
