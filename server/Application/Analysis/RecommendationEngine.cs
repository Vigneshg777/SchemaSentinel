using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;

namespace SchemaSentinel.Application.Analysis;

/// <summary>
/// Produces a de-duplicated, severity-ordered list of recommendations from the
/// findings, with a general closing note.
/// </summary>
public sealed class RecommendationEngine : IRecommendationEngine
{
    public IReadOnlyList<string> Build(IReadOnlyList<RiskFinding> findings)
    {
        var recommendations = findings
            .Where(f => !string.IsNullOrWhiteSpace(f.Recommendation))
            .OrderByDescending(f => f.Severity)
            .Select(f => f.Recommendation!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (findings.Any(f => f.Severity >= RiskSeverity.High))
        {
            recommendations.Add("Test this migration against a recent copy of production data and prepare a rollback plan before deploying.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("No dangerous operations were detected. Review the migration as part of your normal change process.");
        }

        return recommendations;
    }
}
