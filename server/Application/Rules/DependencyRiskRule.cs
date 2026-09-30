using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Rules;

/// <summary>
/// Detects dependency impact: dropping tables/columns or altering columns that
/// other objects depend on. When the database is available it inspects foreign
/// keys that reference the affected table.
/// </summary>
public sealed class DependencyRiskRule : IMigrationRiskRule
{
    public string RuleId => "DEPENDENCY_IMPACT";

    public async Task<IReadOnlyList<RiskFinding>> EvaluateAsync(
        RuleEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<RiskFinding>();

        var impactedTables = context.Migration.Operations
            .Select(GetImpactedTable)
            .Where(t => t is not null)
            .Select(t => t!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var table in impactedTables)
        {
            if (!context.DatabaseAvailable)
            {
                continue;
            }

            if (!await context.Database.TableExistsAsync(table, cancellationToken))
            {
                continue;
            }

            var dependencies = await context.Database.GetDependenciesAsync(table, cancellationToken);
            if (dependencies.Count == 0)
            {
                continue;
            }

            var dependentTables = dependencies
                .Select(d => d.DependentTable)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var evidence = $"{dependentTables.Count} dependent table(s), {dependencies.Count} foreign key dependency(ies):\n"
                + string.Join("\n", dependencies.Select(d => $"{d.DependentTable}.{d.DependentColumn} -> {table}"));

            findings.Add(new RiskFinding
            {
                RuleId = RuleId,
                Title = $"Dependent objects reference '{table}'",
                Severity = RiskSeverity.Medium,
                Category = RiskCategory.Dependency,
                Description = $"'{table}' is referenced by {dependentTables.Count} other table(s) through foreign keys. "
                    + "Dropping or altering it may break those relationships or fail due to the constraints.",
                AffectedObject = table,
                Evidence = evidence,
                Recommendation = "Review and update the dependent foreign keys and tables before changing this object.",
                Source = AnalysisSource.DatabaseAnalysis,
                DatabaseChecked = true,
                DatabaseObjectFound = true
            });
        }

        // Static-only note for explicit foreign key definitions in the script.
        foreach (var fk in context.Migration.Operations.OfType<ForeignKeyOperation>())
        {
            findings.Add(new RiskFinding
            {
                RuleId = RuleId,
                Title = $"New foreign key on '{fk.TableName}'",
                Severity = RiskSeverity.Low,
                Category = RiskCategory.Dependency,
                Description = $"The migration introduces a foreign key from '{fk.TableName}'"
                    + (fk.ReferencedTable is not null ? $" referencing '{fk.ReferencedTable}'." : ".")
                    + " Existing data must satisfy the relationship or the constraint creation will fail.",
                AffectedObject = fk.ReferencedTable is not null ? $"{fk.TableName} -> {fk.ReferencedTable}" : fk.TableName,
                Evidence = fk.Columns.Count > 0 ? $"Columns: {string.Join(", ", fk.Columns)}" : fk.RawStatement,
                Recommendation = "Ensure all existing values have a matching parent row before adding the foreign key.",
                Source = AnalysisSource.StaticAnalysis,
                DatabaseChecked = false,
                DatabaseObjectFound = false
            });
        }

        return findings;
    }

    private static string? GetImpactedTable(MigrationOperation operation) => operation switch
    {
        DropTableOperation drop => drop.TableName,
        DropColumnOperation dropColumn => dropColumn.TableName,
        AlterColumnOperation alter => alter.TableName,
        _ => null
    };
}
