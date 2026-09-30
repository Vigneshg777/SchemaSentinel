namespace SchemaSentinel.Domain.Models;

/// <summary>Summary of how the target database contributed to the analysis.</summary>
public sealed class DatabaseContext
{
    public bool DatabaseChecked { get; init; }
    public string? DatabaseName { get; init; }
    public int ObjectsFound { get; init; }
    public int ObjectsNotFound { get; init; }
}

public sealed class AffectedObject
{
    public required string Name { get; init; }
    public required string ObjectType { get; init; }
    public bool ExistsInDatabase { get; init; }
}
