using SchemaSentinel.Application.Interfaces;
using SchemaSentinel.Application.Rules;
using SchemaSentinel.Domain.Enums;
using SchemaSentinel.Infrastructure.Parsing;
using Xunit;

namespace SchemaSentinel.Tests;

public sealed class RiskRuleTests
{
    private static readonly ScriptDomMigrationParser Parser = new();

    private static RuleEvaluationContext Context(string script, FakeDatabaseMetadataAnalyzer? db = null)
    {
        db ??= new FakeDatabaseMetadataAnalyzer { Available = false };
        return new RuleEvaluationContext
        {
            Migration = Parser.Parse(script),
            Database = db,
            DatabaseAvailable = db.Available
        };
    }

    private static async Task<IReadOnlyList<Domain.Models.RiskFinding>> Run(
        IMigrationRiskRule rule, RuleEvaluationContext context)
        => await rule.EvaluateAsync(context, CancellationToken.None);

    // 1. DROP TABLE
    [Fact]
    public async Task DropTable_static_is_high()
    {
        var findings = await Run(new DropTableRiskRule(), Context("DROP TABLE Customers;"));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.High, f.Severity);
        Assert.Equal(RiskCategory.Destructive, f.Category);
        Assert.Equal(AnalysisSource.StaticAnalysis, f.Source);
    }

    [Fact]
    public async Task DropTable_database_aware_reports_row_count()
    {
        var db = new FakeDatabaseMetadataAnalyzer();
        db.RowCounts["Customers"] = 15;
        var findings = await Run(new DropTableRiskRule(), Context("DROP TABLE Customers;", db));
        var f = Assert.Single(findings);
        Assert.Equal(AnalysisSource.DatabaseAnalysis, f.Source);
        Assert.True(f.DatabaseObjectFound);
        Assert.Contains("15", f.Evidence);
    }

    // 2. DROP COLUMN
    [Fact]
    public async Task DropColumn_static_is_high()
    {
        var findings = await Run(new DropColumnRiskRule(), Context("ALTER TABLE Customers DROP COLUMN Email;"));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.High, f.Severity);
        Assert.Equal(RiskCategory.DataLoss, f.Category);
    }

    [Fact]
    public async Task DropColumn_database_aware_marks_found()
    {
        var db = new FakeDatabaseMetadataAnalyzer();
        db.Columns["Customers.Email"] = new ColumnMetadata("Email", "varchar", 100, true);
        db.RowCounts["Customers"] = 15;
        var findings = await Run(new DropColumnRiskRule(), Context("ALTER TABLE Customers DROP COLUMN Email;", db));
        var f = Assert.Single(findings);
        Assert.True(f.DatabaseObjectFound);
        Assert.Equal(AnalysisSource.DatabaseAnalysis, f.Source);
    }

    // 3. Column narrowing
    [Fact]
    public async Task Narrowing_static_is_medium()
    {
        var findings = await Run(new ColumnNarrowingRiskRule(),
            Context("ALTER TABLE Customers ALTER COLUMN Email VARCHAR(50);"));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.Medium, f.Severity);
    }

    [Fact]
    public async Task Narrowing_with_offending_data_is_high()
    {
        var db = new FakeDatabaseMetadataAnalyzer();
        db.Columns["Customers.Email"] = new ColumnMetadata("Email", "varchar", 100, true);
        db.ExceedingCounts["Customers.Email"] = 14;
        var findings = await Run(new ColumnNarrowingRiskRule(),
            Context("ALTER TABLE Customers ALTER COLUMN Email VARCHAR(50);", db));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.High, f.Severity);
        Assert.Contains("14", f.Evidence);
    }

    [Fact]
    public async Task Widening_is_not_flagged_when_database_known()
    {
        var db = new FakeDatabaseMetadataAnalyzer();
        db.Columns["Customers.Email"] = new ColumnMetadata("Email", "varchar", 50, true);
        var findings = await Run(new ColumnNarrowingRiskRule(),
            Context("ALTER TABLE Customers ALTER COLUMN Email VARCHAR(100);", db));
        Assert.Empty(findings);
    }

    // 4. NULL -> NOT NULL
    [Fact]
    public async Task Nullability_static_is_medium()
    {
        var findings = await Run(new NullabilityChangeRiskRule(),
            Context("ALTER TABLE Customers ALTER COLUMN Email VARCHAR(100) NOT NULL;"));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.Medium, f.Severity);
    }

    [Fact]
    public async Task Nullability_with_existing_nulls_is_high()
    {
        var db = new FakeDatabaseMetadataAnalyzer();
        db.Columns["Customers.Email"] = new ColumnMetadata("Email", "varchar", 100, true);
        db.NullCounts["Customers.Email"] = 23;
        var findings = await Run(new NullabilityChangeRiskRule(),
            Context("ALTER TABLE Customers ALTER COLUMN Email VARCHAR(100) NOT NULL;", db));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.High, f.Severity);
        Assert.Contains("23", f.Evidence);
    }

    // 5. ADD NOT NULL column
    [Fact]
    public async Task RequiredColumn_static_is_medium()
    {
        var findings = await Run(new RequiredColumnRiskRule(),
            Context("ALTER TABLE Orders ADD PaymentReference VARCHAR(100) NOT NULL;"));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.Medium, f.Severity);
    }

    [Fact]
    public async Task RequiredColumn_on_populated_table_is_high()
    {
        var db = new FakeDatabaseMetadataAnalyzer();
        db.RowCounts["Orders"] = 10;
        var findings = await Run(new RequiredColumnRiskRule(),
            Context("ALTER TABLE Orders ADD PaymentReference VARCHAR(100) NOT NULL;", db));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.High, f.Severity);
    }

    [Fact]
    public async Task RequiredColumn_with_default_is_not_flagged()
    {
        var findings = await Run(new RequiredColumnRiskRule(),
            Context("ALTER TABLE Orders ADD Status VARCHAR(20) NOT NULL DEFAULT 'Pending';"));
        Assert.Empty(findings);
    }

    // 6. Dependency detection
    [Fact]
    public async Task Dependency_database_aware_reports_dependents()
    {
        var db = new FakeDatabaseMetadataAnalyzer();
        db.RowCounts["Customers"] = 15;
        db.Dependencies["Customers"] =
        [
            new ForeignKeyDependency("FK_Orders_Customers", "Orders", "CustomerId")
        ];
        var findings = await Run(new DependencyRiskRule(), Context("DROP TABLE Customers;", db));
        var f = Assert.Single(findings);
        Assert.Equal(RiskCategory.Dependency, f.Category);
        Assert.Contains("Orders", f.Evidence);
    }

    // 7. Index operation
    [Fact]
    public async Task Index_operation_is_low_by_default()
    {
        var findings = await Run(new IndexOperationRiskRule(),
            Context("CREATE INDEX IX_Orders_CustomerId ON Orders (CustomerId);"));
        var f = Assert.Single(findings);
        Assert.Equal(RiskSeverity.Low, f.Severity);
        Assert.Equal(RiskCategory.Performance, f.Category);
    }

    // 8. Object does not exist
    [Fact]
    public async Task DropColumn_on_missing_object_falls_back_to_static()
    {
        var db = new FakeDatabaseMetadataAnalyzer(); // available but empty catalog
        var findings = await Run(new DropColumnRiskRule(),
            Context("ALTER TABLE EmployeeRecords DROP COLUMN Salary;", db));
        var f = Assert.Single(findings);
        Assert.True(f.DatabaseChecked);
        Assert.False(f.DatabaseObjectFound);
        Assert.Equal(AnalysisSource.StaticAnalysis, f.Source);
    }
}
