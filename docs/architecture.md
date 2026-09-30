# Architecture & extension guide

## Request flow

1. Angular posts `{ script, scriptName }` to `POST /api/analysis`.
2. `AnalysisService` validates the request (non-empty, size limit).
3. `ScriptDomMigrationParser` parses the script into typed `MigrationOperation`s.
   The script is only ever tokenised and walked — never executed.
4. `SqlServerMetadataAnalyzer.IsAvailableAsync` decides whether database-aware
   analysis is possible.
5. Each registered `IMigrationRiskRule` evaluates the parsed migration and may
   consult the read-only metadata analyzer. Rules are independent.
6. Findings are aggregated; overall severity is the highest meaningful finding.
7. `RecommendationEngine` produces de-duplicated, severity-ordered guidance.
8. The result is returned and saved to `SchemaSentinelDb` via
   `AnalysisHistoryService`.

## Key abstractions

| Abstraction | Responsibility |
| --- | --- |
| `IMigrationParser` | Parse SQL text into `ParsedMigration` (operations + referenced objects + parse errors) |
| `IMigrationRiskRule` | A single, independently testable risk rule |
| `IDatabaseMetadataAnalyzer` | Read-only, parameterized metadata/data checks |
| `IRecommendationEngine` | Build actionable recommendations from findings |
| `IAnalysisService` | Orchestrate the full pipeline |
| `IAnalysisHistoryService` | Persist and read analysis history |

## Adding a new risk rule

1. Create a class in `server/Application/Rules/` implementing `IMigrationRiskRule`.
2. Inspect `context.Migration.Operations` for the operation types you care about.
3. Use `context.DatabaseAvailable` and `context.Database` for optional
   database-aware evidence. Set `Source`, `DatabaseChecked` and
   `DatabaseObjectFound` on each finding accordingly.
4. Register the rule in `Program.cs`:
   `builder.Services.AddScoped<IMigrationRiskRule, YourNewRule>();`
5. Add unit tests in `tests/SchemaSentinel.Tests`.

No existing rule needs to change — the analyzer discovers rules through DI.

## Safety notes

- Object and column names are resolved against system catalog views with
  parameters, then quoted with `QUOTENAME` before any dynamic `COUNT` query.
- Row counts use `sys.dm_db_partition_stats` (no table scan).
- User SQL never becomes an executed command.
