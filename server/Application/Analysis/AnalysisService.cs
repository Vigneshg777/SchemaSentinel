using Microsoft.Extensions.Logging;
using SchemaSentinel.Application.Interfaces;
using SchemaSentinel.Application.Rules;
using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;

namespace SchemaSentinel.Application.Analysis;

/// <summary>
/// Coordinates the full analysis pipeline: parse -> evaluate rules ->
/// database-aware enrichment -> severity, summary and recommendations.
/// The submitted script is only ever parsed, never executed.
/// </summary>
public sealed class AnalysisService(
    IMigrationParser parser,
    IEnumerable<IMigrationRiskRule> rules,
    IDatabaseMetadataAnalyzer database,
    IRecommendationEngine recommendationEngine,
    ILogger<AnalysisService> logger) : IAnalysisService
{
    public const int MaxScriptLength = 100_000;

    public async Task<AnalysisResult> AnalyzeAsync(AnalyzeMigrationRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var scriptName = string.IsNullOrWhiteSpace(request.ScriptName) ? "migration.sql" : request.ScriptName!.Trim();
        logger.LogInformation("Analysis started for script '{ScriptName}'.", scriptName);

        var parsed = parser.Parse(request.Script);
        var databaseAvailable = await database.IsAvailableAsync(cancellationToken);

        var context = new RuleEvaluationContext
        {
            Migration = parsed,
            Database = database,
            DatabaseAvailable = databaseAvailable
        };

        var findings = new List<RiskFinding>();
        foreach (var rule in rules)
        {
            try
            {
                findings.AddRange(await rule.EvaluateAsync(context, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Rule {RuleId} failed and was skipped.", rule.RuleId);
            }
        }

        if (parsed.HasErrors)
        {
            findings.Add(BuildParseErrorFinding(parsed.Errors));
        }

        var (affectedObjects, objectsFound, objectsNotFound) =
            await BuildAffectedObjectsAsync(parsed.ReferencedObjects, databaseAvailable, cancellationToken);

        var overallSeverity = findings.Count > 0 ? findings.Max(f => f.Severity) : RiskSeverity.Info;
        var summary = BuildSummary(findings, overallSeverity, databaseAvailable);
        var recommendations = recommendationEngine.Build(findings);

        var result = new AnalysisResult
        {
            AnalysisId = Guid.NewGuid(),
            OverallSeverity = overallSeverity,
            Summary = summary,
            Findings = findings
                .OrderByDescending(f => f.Severity)
                .ToList(),
            AffectedObjects = affectedObjects,
            DatabaseContext = new DatabaseContext
            {
                DatabaseChecked = databaseAvailable,
                DatabaseName = databaseAvailable ? database.DatabaseName : null,
                ObjectsFound = objectsFound,
                ObjectsNotFound = objectsNotFound
            },
            Recommendations = recommendations,
            ScriptName = scriptName
        };

        logger.LogInformation(
            "Analysis completed for '{ScriptName}' with severity {Severity} and {Count} finding(s).",
            scriptName, overallSeverity, findings.Count);

        return result;
    }

    private static void Validate(AnalyzeMigrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Script))
        {
            throw new MigrationValidationException("The migration script cannot be empty.");
        }

        if (request.Script.Length > MaxScriptLength)
        {
            throw new MigrationValidationException(
                $"The migration script exceeds the maximum supported size of {MaxScriptLength:N0} characters.");
        }
    }

    private async Task<(IReadOnlyList<AffectedObject> Objects, int Found, int NotFound)> BuildAffectedObjectsAsync(
        IReadOnlyList<string> referencedObjects,
        bool databaseAvailable,
        CancellationToken cancellationToken)
    {
        var objects = new List<AffectedObject>();
        var found = 0;
        var notFound = 0;

        foreach (var name in referencedObjects)
        {
            var exists = false;
            if (databaseAvailable)
            {
                exists = await database.TableExistsAsync(name, cancellationToken);
                if (exists)
                {
                    found++;
                }
                else
                {
                    notFound++;
                }
            }

            objects.Add(new AffectedObject
            {
                Name = name,
                ObjectType = "Table",
                ExistsInDatabase = exists
            });
        }

        return (objects, found, notFound);
    }

    private static RiskFinding BuildParseErrorFinding(IReadOnlyList<Domain.Parsing.ParseError> errors)
    {
        var evidence = string.Join("\n", errors.Take(10).Select(e => $"Line {e.Line}, Col {e.Column}: {e.Message}"));
        return new RiskFinding
        {
            RuleId = "PARSE_WARNING",
            Title = "The script could not be fully parsed",
            Severity = RiskSeverity.Low,
            Category = RiskCategory.Compatibility,
            Description = "Part of the migration could not be parsed as valid SQL Server syntax. "
                + "Analysis continued on the portion that was understood, but some risks may not have been detected.",
            Evidence = evidence,
            Recommendation = "Review the reported syntax issues and confirm the script targets SQL Server.",
            Source = AnalysisSource.StaticAnalysis,
            DatabaseChecked = false,
            DatabaseObjectFound = false
        };
    }

    private static string BuildSummary(
        IReadOnlyList<RiskFinding> findings,
        RiskSeverity overallSeverity,
        bool databaseAvailable)
    {
        if (findings.Count == 0)
        {
            return "No risky operations were detected in the migration script.";
        }

        var high = findings.Count(f => f.Severity >= RiskSeverity.High);
        var context = databaseAvailable
            ? "Static and target-database analysis were both applied."
            : "Only static SQL analysis was applied because the target database was not available.";

        var lead = high > 0
            ? $"The migration contains {high} high-risk operation(s) that could cause data loss or a failed deployment."
            : "The migration contains operations that warrant review before deployment.";

        return $"{lead} Overall risk is {overallSeverity.ToString().ToUpperInvariant()}. {context}";
    }
}
