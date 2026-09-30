namespace SchemaSentinel.Domain.Enums;

/// <summary>
/// Explainable severity levels ordered from least to most severe.
/// Overall severity is the highest meaningful finding.
/// </summary>
public enum RiskSeverity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
