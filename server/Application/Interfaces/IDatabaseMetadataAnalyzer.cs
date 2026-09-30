namespace SchemaSentinel.Application.Interfaces;

public sealed record ColumnMetadata(
    string Name,
    string DataType,
    int? MaxLength,
    bool IsNullable);

public sealed record ForeignKeyDependency(
    string ForeignKeyName,
    string DependentTable,
    string? DependentColumn);

public sealed record IndexMetadata(
    string Name,
    bool IsPrimaryKey,
    bool IsUnique);

/// <summary>
/// Provides read-only, parameterized metadata and data checks against the
/// target database. Never executes user-submitted migration SQL.
/// </summary>
public interface IDatabaseMetadataAnalyzer
{
    string DatabaseName { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    Task<bool> TableExistsAsync(string table, CancellationToken cancellationToken);

    Task<ColumnMetadata?> GetColumnAsync(string table, string column, CancellationToken cancellationToken);

    Task<long?> GetRowCountAsync(string table, CancellationToken cancellationToken);

    Task<long?> GetNullCountAsync(string table, string column, CancellationToken cancellationToken);

    Task<long?> CountValuesExceedingLengthAsync(string table, string column, int length, CancellationToken cancellationToken);

    Task<IReadOnlyList<ForeignKeyDependency>> GetDependenciesAsync(string table, CancellationToken cancellationToken);

    Task<IReadOnlyList<IndexMetadata>> GetIndexesAsync(string table, CancellationToken cancellationToken);
}
