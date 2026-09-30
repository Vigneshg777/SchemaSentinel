using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Rules;

/// <summary>
/// Detects column narrowing (reducing a character length) which can truncate
/// data and fail deployment. When the database is available it compares the
/// current length and counts values that would not fit.
/// </summary>
public sealed class ColumnNarrowingRiskRule : IMigrationRiskRule
{
    public string RuleId => "COLUMN_NARROWING";

    public async Task<IReadOnlyList<RiskFinding>> EvaluateAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<RiskFinding>();

        foreach (var op in context.Migration.Operations.OfType<AlterColumnOperation>())
        {
            if (op.Length is not int newLength || op.IsMaxLength)
            {
                continue;
            }

            var checkedDb = false;
            var found = false;
            int? currentLength = null;
            long? exceeding = null;

            if (context.DatabaseAvailable)
            {
                checkedDb = true;
                var column = await context.Database.GetColumnAsync(op.TableName, op.ColumnName, cancellationToken);
                if (column is not null)
                {
                    found = true;
                    currentLength = column.MaxLength;
                    if (currentLength is int existing && existing > newLength)
                    {
                        exceeding = await context.Database.CountValuesExceedingLengthAsync(
                            op.TableName, op.ColumnName, newLength, cancellationToken);
                    }
                }
            }

            // Skip when the database confirms this is a widening (or same length) change.
            if (found && currentLength is int known && known <= newLength)
            {
                continue;
            }

            var hasProblemData = exceeding is > 0;
            var severity = hasProblemData ? RiskSeverity.High : RiskSeverity.Medium;

            string evidence;
            if (found && currentLength is not null)
            {
                evidence = $"Current length: {currentLength}\nNew length: {newLength}"
                    + (exceeding is not null ? $"\nRows exceeding {newLength} characters: {exceeding:N0}" : string.Empty);
            }
            else
            {
                evidence = $"Statement reduces '{op.ColumnName}' to {op.DataType}({newLength}).";
            }

            findings.Add(new RiskFinding
            {
                RuleId = RuleId,
                Title = $"Column narrowing on '{op.TableName}.{op.ColumnName}'",
                Severity = severity,
                Category = RiskCategory.DataLoss,
                Description = hasProblemData
                    ? $"Reducing '{op.ColumnName}' to length {newLength} would truncate existing values and the deployment could fail."
                    : $"Reducing the length of '{op.ColumnName}' to {newLength} may truncate longer values or fail if any exceed the new limit.",
                AffectedObject = $"{op.TableName}.{op.ColumnName}",
                Evidence = evidence,
                Recommendation = hasProblemData
                    ? "Shorten or migrate the oversized values before applying the change, or keep the wider type."
                    : "Confirm no existing or future values exceed the new length before narrowing the column.",
                Source = checkedDb && found ? AnalysisSource.DatabaseAnalysis : AnalysisSource.StaticAnalysis,
                DatabaseChecked = checkedDb,
                DatabaseObjectFound = found
            });
        }

        return findings;
    }
}
