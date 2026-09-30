namespace SchemaSentinel.Domain.Enums;

/// <summary>
/// Indicates whether a finding was produced from the SQL text alone
/// or enriched with read-only checks against the target database.
/// </summary>
public enum AnalysisSource
{
    StaticAnalysis,
    DatabaseAnalysis
}
