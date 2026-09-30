using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Rules;

/// <summary>Flags DROP TABLE operations as destructive with potential permanent data loss.</summary>
public sealed class DropTableRiskRule : IMigrationRiskRule
{
    public string RuleId => "DROP_TABLE";

    public async Task<IReadOnlyList<RiskFinding>> EvaluateAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<RiskFinding>();

        foreach (var op in context.Migration.Operations.OfType<DropTableOperation>())
        {
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

            var evidence = found
                ? $"Table '{op.TableName}' exists in {context.Database.DatabaseName}"
                    + (rowCount is not null ? $" and currently holds {rowCount:N0} row(s)." : ".")
                : $"Statement drops table '{op.TableName}'.";

            findings.Add(new RiskFinding
            {
                RuleId = RuleId,
                Title = $"DROP TABLE on '{op.TableName}'",
                Severity = RiskSeverity.High,
                Category = RiskCategory.Destructive,
                Description = $"The migration permanently removes the table '{op.TableName}'. "
                    + "All rows, indexes and constraints on this table would be lost and the operation cannot be undone once deployed.",
                AffectedObject = op.TableName,
                Evidence = evidence,
                Recommendation = "Confirm the table is truly obsolete. Back up the data and verify no application, view or foreign key still depends on it before dropping.",
                Source = checkedDb && found ? AnalysisSource.DatabaseAnalysis : AnalysisSource.StaticAnalysis,
                DatabaseChecked = checkedDb,
                DatabaseObjectFound = found
            });
        }

        return findings;
    }
}
