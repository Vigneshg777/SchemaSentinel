using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Application.Interfaces;

/// <summary>
/// Parses a SQL Server migration script into a structured set of operations.
/// Implementations must never execute the submitted SQL.
/// </summary>
public interface IMigrationParser
{
    ParsedMigration Parse(string script);
}
