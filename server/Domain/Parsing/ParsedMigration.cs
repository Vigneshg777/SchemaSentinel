namespace SchemaSentinel.Domain.Parsing;

public sealed record ParseError(int Line, int Column, string Message);

/// <summary>
/// Result of parsing a migration script: the extracted operations, the set of
/// referenced database objects, and any parse errors encountered.
/// </summary>
public sealed class ParsedMigration
{
    public required IReadOnlyList<MigrationOperation> Operations { get; init; }
    public required IReadOnlyList<string> ReferencedObjects { get; init; }
    public required IReadOnlyList<ParseError> Errors { get; init; }

    public bool HasErrors => Errors.Count > 0;
}
