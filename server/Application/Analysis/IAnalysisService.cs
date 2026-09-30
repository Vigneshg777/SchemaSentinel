using SchemaSentinel.Domain.Models;

namespace SchemaSentinel.Application.Analysis;

/// <summary>Orchestrates parsing, rule evaluation and database-aware analysis.</summary>
public interface IAnalysisService
{
    Task<AnalysisResult> AnalyzeAsync(AnalyzeMigrationRequest request, CancellationToken cancellationToken);
}

/// <summary>Reads and persists analysis history in SchemaSentinelDb.</summary>
public interface IAnalysisHistoryService
{
    Task SaveAsync(AnalysisResult result, string scriptText, CancellationToken cancellationToken);

    Task<IReadOnlyList<AnalysisHistoryItem>> GetRecentAsync(int take, CancellationToken cancellationToken);

    Task<AnalysisResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Lightweight history list projection.</summary>
public sealed record AnalysisHistoryItem(
    Guid Id,
    DateTimeOffset CreatedAt,
    string ScriptName,
    Domain.Enums.RiskSeverity OverallSeverity,
    string Summary);

/// <summary>Builds actionable recommendations from a set of findings.</summary>
public interface IRecommendationEngine
{
    IReadOnlyList<string> Build(IReadOnlyList<RiskFinding> findings);
}
