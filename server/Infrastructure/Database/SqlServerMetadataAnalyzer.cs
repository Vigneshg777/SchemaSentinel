using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SchemaSentinel.Application.Interfaces;

namespace SchemaSentinel.Infrastructure.Database;

/// <summary>
/// Read-only metadata analyzer for the target database. All object/column
/// names are first resolved against system catalog views using parameterized
/// queries; any dynamic count queries use QUOTENAME on catalog-verified names
/// so submitted SQL can never be executed.
/// </summary>
public sealed class SqlServerMetadataAnalyzer : IDatabaseMetadataAnalyzer
{
    private readonly string? _connectionString;
    private readonly ILogger<SqlServerMetadataAnalyzer> _logger;

    public SqlServerMetadataAnalyzer(IConfiguration configuration, ILogger<SqlServerMetadataAnalyzer> logger)
    {
        _connectionString = configuration.GetConnectionString("DemoTargetDb");
        _logger = logger;

        DatabaseName = TryGetDatabaseName(_connectionString);
    }

    public string DatabaseName { get; }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return false;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Target database is not available for metadata analysis.");
            return false;
        }
    }

    public async Task<bool> TableExistsAsync(string table, CancellationToken cancellationToken)
    {
        var resolved = await ResolveTableAsync(table, cancellationToken);
        return resolved is not null;
    }

    public async Task<ColumnMetadata?> GetColumnAsync(string table, string column, CancellationToken cancellationToken)
    {
        var resolved = await ResolveTableAsync(table, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        const string sql = """
            SELECT ty.name AS DataType, c.max_length AS MaxLength, c.is_nullable AS IsNullable
            FROM sys.columns c
            JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE c.object_id = @objectId AND c.name = @column;
            """;

        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@objectId", resolved.ObjectId);
            command.Parameters.AddWithValue("@column", column);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var dataType = reader.GetString(0);
            var rawMaxLength = reader.GetInt16(1);
            var isNullable = reader.GetBoolean(2);

            return new ColumnMetadata(column, dataType, NormalizeLength(dataType, rawMaxLength), isNullable);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to read column metadata for {Table}.{Column}.", table, column);
            return null;
        }
    }

    public async Task<long?> GetRowCountAsync(string table, CancellationToken cancellationToken)
    {
        var resolved = await ResolveTableAsync(table, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        // Uses partition stats, so no scan of the table's data is required.
        const string sql = """
            SELECT SUM(ps.row_count)
            FROM sys.dm_db_partition_stats ps
            WHERE ps.object_id = @objectId AND ps.index_id IN (0, 1);
            """;

        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@objectId", resolved.ObjectId);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? 0 : Convert.ToInt64(result);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to read row count for {Table}.", table);
            return null;
        }
    }

    public async Task<long?> GetNullCountAsync(string table, string column, CancellationToken cancellationToken)
    {
        var resolved = await ResolveTableAsync(table, cancellationToken);
        if (resolved is null || !await ColumnExistsAsync(resolved.ObjectId, column, cancellationToken))
        {
            return null;
        }

        const string sql = """
            DECLARE @stmt NVARCHAR(MAX) =
                N'SELECT COUNT_BIG(*) FROM ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) +
                N' WHERE ' + QUOTENAME(@column) + N' IS NULL';
            EXEC sys.sp_executesql @stmt;
            """;

        return await ExecuteScalarCountAsync(sql, resolved, column, parameters: null, cancellationToken);
    }

    public async Task<long?> CountValuesExceedingLengthAsync(string table, string column, int length, CancellationToken cancellationToken)
    {
        var resolved = await ResolveTableAsync(table, cancellationToken);
        if (resolved is null || !await ColumnExistsAsync(resolved.ObjectId, column, cancellationToken))
        {
            return null;
        }

        const string sql = """
            DECLARE @stmt NVARCHAR(MAX) =
                N'SELECT COUNT_BIG(*) FROM ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) +
                N' WHERE LEN(' + QUOTENAME(@column) + N') > @len';
            EXEC sys.sp_executesql @stmt, N'@len INT', @len = @length;
            """;

        return await ExecuteScalarCountAsync(
            sql,
            resolved,
            column,
            parameters: cmd => cmd.Parameters.AddWithValue("@length", length),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ForeignKeyDependency>> GetDependenciesAsync(string table, CancellationToken cancellationToken)
    {
        var resolved = await ResolveTableAsync(table, cancellationToken);
        if (resolved is null)
        {
            return [];
        }

        const string sql = """
            SELECT fk.name AS ForeignKeyName, pt.name AS DependentTable, pc.name AS DependentColumn
            FROM sys.foreign_keys fk
            JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            WHERE fk.referenced_object_id = @objectId;
            """;

        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@objectId", resolved.ObjectId);

            var dependencies = new List<ForeignKeyDependency>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                dependencies.Add(new ForeignKeyDependency(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }

            return dependencies;
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to read dependencies for {Table}.", table);
            return [];
        }
    }

    public async Task<IReadOnlyList<IndexMetadata>> GetIndexesAsync(string table, CancellationToken cancellationToken)
    {
        var resolved = await ResolveTableAsync(table, cancellationToken);
        if (resolved is null)
        {
            return [];
        }

        const string sql = """
            SELECT i.name, i.is_primary_key, i.is_unique
            FROM sys.indexes i
            WHERE i.object_id = @objectId AND i.type <> 0 AND i.name IS NOT NULL;
            """;

        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@objectId", resolved.ObjectId);

            var indexes = new List<IndexMetadata>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                indexes.Add(new IndexMetadata(
                    reader.GetString(0),
                    reader.GetBoolean(1),
                    reader.GetBoolean(2)));
            }

            return indexes;
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to read indexes for {Table}.", table);
            return [];
        }
    }

    private async Task<long?> ExecuteScalarCountAsync(
        string sql,
        ResolvedTable resolved,
        string column,
        Action<SqlCommand>? parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@schema", resolved.Schema);
            command.Parameters.AddWithValue("@table", resolved.Name);
            command.Parameters.AddWithValue("@column", column);
            parameters?.Invoke(command);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? 0 : Convert.ToInt64(result);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to execute count query for {Table}.{Column}.", resolved.Name, column);
            return null;
        }
    }

    private async Task<bool> ColumnExistsAsync(int objectId, string column, CancellationToken cancellationToken)
    {
        const string sql = "SELECT 1 FROM sys.columns WHERE object_id = @objectId AND name = @column;";
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@objectId", objectId);
            command.Parameters.AddWithValue("@column", column);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    private async Task<ResolvedTable?> ResolveTableAsync(string table, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(table))
        {
            return null;
        }

        const string sql = """
            SELECT TOP 1 s.name AS SchemaName, t.name AS TableName, t.object_id
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.name = @table
            ORDER BY CASE WHEN s.name = 'dbo' THEN 0 ELSE 1 END, s.name;
            """;

        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@table", table);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ResolvedTable(reader.GetString(0), reader.GetString(1), reader.GetInt32(2));
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to resolve table {Table}.", table);
            return null;
        }
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
{
    const int maxAttempts = 3;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        var connection = new SqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (SqlException ex) when (attempt < maxAttempts)
        {
            await connection.DisposeAsync();

            _logger.LogWarning(
                ex,
                "Failed to connect to target database on attempt {Attempt}/{MaxAttempts}. Retrying...",
                attempt,
                maxAttempts);

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    throw new InvalidOperationException("Unable to connect to the target database.");
}
    private static int? NormalizeLength(string dataType, short rawMaxLength)
    {
        if (rawMaxLength == -1)
        {
            return null; // MAX types have no fixed length.
        }

        return dataType is "nchar" or "nvarchar"
            ? rawMaxLength / 2
            : rawMaxLength;
    }

    private static string TryGetDatabaseName(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "DemoTargetDb";
        }

        try
        {
            return new SqlConnectionStringBuilder(connectionString).InitialCatalog is { Length: > 0 } name
                ? name
                : "DemoTargetDb";
        }
        catch (ArgumentException)
        {
            return "DemoTargetDb";
        }
    }

    private sealed record ResolvedTable(string Schema, string Name, int ObjectId);
}
