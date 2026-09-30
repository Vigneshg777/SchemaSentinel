namespace SchemaSentinel.Domain.Parsing;

/// <summary>
/// Base type for a meaningful operation extracted from a migration script.
/// Rules pattern-match on the concrete derived types.
/// </summary>
public abstract record MigrationOperation
{
    /// <summary>The original SQL text fragment this operation was parsed from.</summary>
    public string RawStatement { get; init; } = string.Empty;
}

/// <summary>Nullability intent expressed by a column definition in the script.</summary>
public enum ColumnNullability
{
    Unspecified,
    Nullable,
    NotNull
}

public sealed record DropTableOperation : MigrationOperation
{
    public required string TableName { get; init; }
}

public sealed record DropColumnOperation : MigrationOperation
{
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }
}

public sealed record AlterColumnOperation : MigrationOperation
{
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }
    public string? DataType { get; init; }
    public int? Length { get; init; }
    public bool IsMaxLength { get; init; }
    public ColumnNullability Nullability { get; init; } = ColumnNullability.Unspecified;
}

public sealed record AddColumnOperation : MigrationOperation
{
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }
    public string? DataType { get; init; }
    public int? Length { get; init; }
    public bool IsMaxLength { get; init; }
    public ColumnNullability Nullability { get; init; } = ColumnNullability.Unspecified;
    public bool HasDefault { get; init; }
}

public sealed record ForeignKeyOperation : MigrationOperation
{
    public required string TableName { get; init; }
    public string? ReferencedTable { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = [];
}

public enum IndexOperationKind
{
    Create,
    Drop,
    Alter
}

public sealed record IndexOperation : MigrationOperation
{
    public required IndexOperationKind Kind { get; init; }
    public string? IndexName { get; init; }
    public string? TableName { get; init; }
}
