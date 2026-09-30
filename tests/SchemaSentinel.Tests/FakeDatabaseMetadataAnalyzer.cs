using SchemaSentinel.Application.Interfaces;

namespace SchemaSentinel.Tests;

/// <summary>
/// In-memory stand-in for the database analyzer so rules can be tested with
/// deterministic "database-aware" data without touching a real database.
/// </summary>
public sealed class FakeDatabaseMetadataAnalyzer : IDatabaseMetadataAnalyzer
{
    public string DatabaseName => "FakeDb";

    public bool Available { get; set; } = true;

    public Dictionary<string, long> RowCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ColumnMetadata> Columns { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, long> NullCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, long> ExceedingCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<ForeignKeyDependency>> Dependencies { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<IndexMetadata>> Indexes { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static string Key(string table, string column) => $"{table}.{column}";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(Available);

    public Task<bool> TableExistsAsync(string table, CancellationToken cancellationToken)
        => Task.FromResult(RowCounts.ContainsKey(table)
            || Columns.Keys.Any(k => k.StartsWith($"{table}.", StringComparison.OrdinalIgnoreCase))
            || Dependencies.ContainsKey(table)
            || Indexes.ContainsKey(table));

    public Task<ColumnMetadata?> GetColumnAsync(string table, string column, CancellationToken cancellationToken)
        => Task.FromResult(Columns.TryGetValue(Key(table, column), out var meta) ? meta : null);

    public Task<long?> GetRowCountAsync(string table, CancellationToken cancellationToken)
        => Task.FromResult<long?>(RowCounts.TryGetValue(table, out var count) ? count : null);

    public Task<long?> GetNullCountAsync(string table, string column, CancellationToken cancellationToken)
        => Task.FromResult<long?>(NullCounts.TryGetValue(Key(table, column), out var count) ? count : 0);

    public Task<long?> CountValuesExceedingLengthAsync(string table, string column, int length, CancellationToken cancellationToken)
        => Task.FromResult<long?>(ExceedingCounts.TryGetValue(Key(table, column), out var count) ? count : 0);

    public Task<IReadOnlyList<ForeignKeyDependency>> GetDependenciesAsync(string table, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ForeignKeyDependency>>(
            Dependencies.TryGetValue(table, out var deps) ? deps : []);

    public Task<IReadOnlyList<IndexMetadata>> GetIndexesAsync(string table, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<IndexMetadata>>(
            Indexes.TryGetValue(table, out var idx) ? idx : []);
}
