using Microsoft.Extensions.Logging.Abstractions;
using SchemaSentinel.Application.Analysis;
using SchemaSentinel.Application.Rules;
using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Infrastructure.Parsing;
using Xunit;

namespace SchemaSentinel.Tests;

public sealed class AnalysisServiceTests
{
    private static AnalysisService CreateService(FakeDatabaseMetadataAnalyzer db)
    {
        IEnumerable<IMigrationRiskRule> rules =
        [
            new DropTableRiskRule(),
            new DropColumnRiskRule(),
            new ColumnNarrowingRiskRule(),
            new NullabilityChangeRiskRule(),
            new RequiredColumnRiskRule(),
            new DependencyRiskRule(),
            new IndexOperationRiskRule()
        ];

        return new AnalysisService(
            new ScriptDomMigrationParser(),
            rules,
            db,
            new RecommendationEngine(),
            NullLogger<AnalysisService>.Instance);
    }

    // 10. Empty SQL
    [Fact]
    public async Task Empty_script_throws_validation()
    {
        var service = CreateService(new FakeDatabaseMetadataAnalyzer { Available = false });
        await Assert.ThrowsAsync<MigrationValidationException>(
            () => service.AnalyzeAsync(new AnalyzeMigrationRequest("   ", null), CancellationToken.None));
    }

    // 9. Malformed SQL
    [Fact]
    public async Task Malformed_script_returns_parse_warning()
    {
        var service = CreateService(new FakeDatabaseMetadataAnalyzer { Available = false });
        var result = await service.AnalyzeAsync(
            new AnalyzeMigrationRequest("ALTER TABLE DROP DROP COLUMN;", "bad.sql"), CancellationToken.None);

        Assert.Contains(result.Findings, f => f.RuleId == "PARSE_WARNING");
    }

    [Fact]
    public async Task Overall_severity_is_highest_finding()
    {
        var db = new FakeDatabaseMetadataAnalyzer { Available = false };
        var service = CreateService(db);
        var result = await service.AnalyzeAsync(
            new AnalyzeMigrationRequest("DROP TABLE Customers;", "drop.sql"), CancellationToken.None);

        Assert.Equal(RiskSeverity.High, result.OverallSeverity);
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public async Task Safe_migration_is_low_risk()
    {
        var db = new FakeDatabaseMetadataAnalyzer { Available = false };
        var service = CreateService(db);
        var result = await service.AnalyzeAsync(
            new AnalyzeMigrationRequest("ALTER TABLE Customers ADD LoyaltyCode VARCHAR(20) NULL;", "safe.sql"),
            CancellationToken.None);

        Assert.True(result.OverallSeverity <= RiskSeverity.Low);
    }

    [Fact]
    public async Task Database_context_reflects_found_objects()
    {
        var db = new FakeDatabaseMetadataAnalyzer();
        db.RowCounts["Customers"] = 15;
        var service = CreateService(db);
        var result = await service.AnalyzeAsync(
            new AnalyzeMigrationRequest("DROP TABLE Customers; DROP TABLE MissingTable;", "mix.sql"),
            CancellationToken.None);

        Assert.True(result.DatabaseContext.DatabaseChecked);
        Assert.Equal(1, result.DatabaseContext.ObjectsFound);
        Assert.Equal(1, result.DatabaseContext.ObjectsNotFound);
    }
}
