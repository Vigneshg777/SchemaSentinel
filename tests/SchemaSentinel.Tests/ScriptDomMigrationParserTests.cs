using SchemaSentinel.Domain.Parsing;
using SchemaSentinel.Infrastructure.Parsing;
using Xunit;

namespace SchemaSentinel.Tests;

public sealed class ScriptDomMigrationParserTests
{
    private readonly ScriptDomMigrationParser _parser = new();

    [Fact]
    public void Parses_drop_table()
    {
        var parsed = _parser.Parse("DROP TABLE Customers;");
        var op = Assert.Single(parsed.Operations.OfType<DropTableOperation>());
        Assert.Equal("Customers", op.TableName);
        Assert.False(parsed.HasErrors);
    }

    [Fact]
    public void Parses_drop_column()
    {
        var parsed = _parser.Parse("ALTER TABLE Customers DROP COLUMN Email;");
        var op = Assert.Single(parsed.Operations.OfType<DropColumnOperation>());
        Assert.Equal("Customers", op.TableName);
        Assert.Equal("Email", op.ColumnName);
    }

    [Fact]
    public void Parses_alter_column_with_length_and_not_null()
    {
        var parsed = _parser.Parse("ALTER TABLE Customers ALTER COLUMN Email VARCHAR(50) NOT NULL;");
        var op = Assert.Single(parsed.Operations.OfType<AlterColumnOperation>());
        Assert.Equal("Email", op.ColumnName);
        Assert.Equal(50, op.Length);
        Assert.Equal(ColumnNullability.NotNull, op.Nullability);
    }

    [Fact]
    public void Parses_add_required_column()
    {
        var parsed = _parser.Parse("ALTER TABLE Orders ADD PaymentReference VARCHAR(100) NOT NULL;");
        var op = Assert.Single(parsed.Operations.OfType<AddColumnOperation>());
        Assert.Equal("PaymentReference", op.ColumnName);
        Assert.Equal(ColumnNullability.NotNull, op.Nullability);
        Assert.False(op.HasDefault);
    }

    [Fact]
    public void Parses_nullable_add_column()
    {
        var parsed = _parser.Parse("ALTER TABLE Customers ADD LoyaltyCode VARCHAR(20) NULL;");
        var op = Assert.Single(parsed.Operations.OfType<AddColumnOperation>());
        Assert.Equal(ColumnNullability.Nullable, op.Nullability);
    }

    [Fact]
    public void Parses_index_operations()
    {
        var parsed = _parser.Parse("CREATE INDEX IX_Orders_CustomerId ON Orders (CustomerId);");
        var op = Assert.Single(parsed.Operations.OfType<IndexOperation>());
        Assert.Equal(IndexOperationKind.Create, op.Kind);
        Assert.Equal("Orders", op.TableName);
    }

    [Fact]
    public void Collects_referenced_objects()
    {
        var parsed = _parser.Parse("DROP TABLE Customers; ALTER TABLE Orders DROP COLUMN Notes;");
        Assert.Contains("Customers", parsed.ReferencedObjects);
        Assert.Contains("Orders", parsed.ReferencedObjects);
    }

    [Fact]
    public void Reports_errors_for_malformed_sql()
    {
        var parsed = _parser.Parse("ALTER TABLE DROP DROP COLUMN;");
        Assert.True(parsed.HasErrors);
    }

    [Fact]
    public void Empty_script_yields_no_operations()
    {
        var parsed = _parser.Parse(string.Empty);
        Assert.Empty(parsed.Operations);
        Assert.False(parsed.HasErrors);
    }
}
