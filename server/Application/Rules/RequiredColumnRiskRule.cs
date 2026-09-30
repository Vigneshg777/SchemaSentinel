using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Rules;

/// <summary>
/// Detects adding a NOT NULL column without a default to a table. Populated
/// tables cannot accept such a column and the deployment would fail.
/// </summary>
public sealed class RequiredColumnRiskRule : IMigrationRiskRule
{
    public string RuleId => "REQUIRED_COLUMN";

    public async Task<IReadOnlyList<RiskFinding>> EvaluateAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<RiskFinding>();

        foreach (var op in context.Migration.Operations.OfType<AddColumnOperation>())
        {
            if (op.Nullability != ColumnNullability.NotNull || op.HasDefault)
            {
                continue;
            }

            var checkedDb = false;
            var found = false;
            long? rowCount = null;

            if (context.DatabaseAvailable)
            {
                checkedDb = true;
                found = await context.Database.TableExistsAsync(op.TableName, cancellationToken);
                if (found)
                {
                    rowCount = await context.Database.GetRowCountAsync(op.TableName, cancellationToken);
                }
            }

            var populated = rowCount is > 0;
            var severity = populated ? RiskSeverity.High : RiskSeverity.Medium;

            var evidence = found
                ? (rowCount is not null
                    ? $"Table '{op.TableName}' currently holds {rowCount:N0} row(s)."
                    : $"Table '{op.TableName}' exists in {context.Database.DatabaseName}.")
                : $"Statement adds required column '{op.ColumnName}' to '{op.TableName}'.";

            findings.Add(new RiskFinding
            {
                RuleId = RuleId,
                Title = $"NOT NULL column '{op.ColumnName}' added to '{op.TableName}'",
                Severity = severity,
                Category = RiskCategory.Constraint,
                Description = populated
                    ? $"Adding required column '{op.ColumnName}' to the populated table '{op.TableName}' will fail because existing rows have no value for it."
                    : $"Adding a NOT NULL column '{op.ColumnName}' without a default fails on any table that already contains rows.",
                AffectedObject = $"{op.TableName}.{op.ColumnName}",
                Evidence = evidence,
                Recommendation = "Add the column as nullable or with a safe default, backfill existing rows, then enforce NOT NULL.",
                Source = checkedDb && found ? AnalysisSource.DatabaseAnalysis : AnalysisSource.StaticAnalysis,
                DatabaseChecked = checkedDb,
                DatabaseObjectFound = found
            });
        }

        return findings;
    }
}
