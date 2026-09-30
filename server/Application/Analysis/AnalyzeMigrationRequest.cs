namespace SchemaSentinel.Application.Analysis;

/// <summary>Incoming request to analyze a migration script.</summary>
public sealed record AnalyzeMigrationRequest(string Script, string? ScriptName);

/// <summary>Thrown when a submitted migration request fails validation.</summary>
public sealed class MigrationValidationException(string message) : Exception(message);
