using Microsoft.EntityFrameworkCore;
using SchemaSentinel.Application.Analysis;
using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Domain.Models;
using SchemaSentinel.Infrastructure.Data;
using SchemaSentinel.Infrastructure.Data.Entities;

namespace SchemaSentinel.Infrastructure.Services;

/// <summary>Persists and reads analysis history from SchemaSentinelDb.</summary>
public sealed class AnalysisHistoryService(SchemaSentinelDbContext dbContext) : IAnalysisHistoryService
{
    public async Task SaveAsync(AnalysisResult result, string scriptText, CancellationToken cancellationToken)
    {
        var entity = new Analysis
        {
            Id = result.AnalysisId,
            CreatedAt = result.CreatedAt,
            ScriptName = result.ScriptName ?? "migration.sql",
            ScriptText = scriptText,
            OverallSeverity = result.OverallSeverity,
            Summary = result.Summary,
            Findings = result.Findings.Select(f => new AnalysisFinding
            {
                Id = f.Id,
                RuleId = f.RuleId,
                Severity = f.Severity,
                Category = f.Category,
                Title = f.Title,
                Description = f.Description,
                AffectedObject = f.AffectedObject,
                Evidence = f.Evidence,
                Recommendation = f.Recommendation,
                Source = f.Source
            }).ToList()
        };

        dbContext.Analyses.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnalysisHistoryItem>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 200);

        return await dbContext.Analyses
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new AnalysisHistoryItem(
                a.Id, a.CreatedAt, a.ScriptName, a.OverallSeverity, a.Summary))
            .ToListAsync(cancellationToken);
    }

    public async Task<AnalysisResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Analyses
            .AsNoTracking()
            .Include(a => a.Findings)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var findings = entity.Findings
            .Select(f => new RiskFinding
            {
                Id = f.Id,
                RuleId = f.RuleId,
                Title = f.Title,
                Severity = f.Severity,
                Category = f.Category,
                Description = f.Description,
                AffectedObject = f.AffectedObject,
                Evidence = f.Evidence,
                Recommendation = f.Recommendation,
                Source = f.Source,
                DatabaseChecked = f.Source == AnalysisSource.DatabaseAnalysis,
                DatabaseObjectFound = f.Source == AnalysisSource.DatabaseAnalysis
            })
            .OrderByDescending(f => f.Severity)
            .ToList();

        return new AnalysisResult
        {
            AnalysisId = entity.Id,
            OverallSeverity = entity.OverallSeverity,
            Summary = entity.Summary,
            Findings = findings,
            AffectedObjects = [],
            DatabaseContext = new DatabaseContext
            {
                DatabaseChecked = findings.Any(f => f.Source == AnalysisSource.DatabaseAnalysis),
                ObjectsFound = 0,
                ObjectsNotFound = 0
            },
            Recommendations = findings
                .Where(f => !string.IsNullOrWhiteSpace(f.Recommendation))
                .Select(f => f.Recommendation!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CreatedAt = entity.CreatedAt,
            ScriptName = entity.ScriptName
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Analyses.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.Analyses.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
