# SchemaSentinel — Complete Application Flow

This document explains SchemaSentinel end to end: what it is, how it is structured,
how a request travels through every layer, how the two databases are used, and how
the project is built, tested and run.

> **Core guarantee:** submitted migration scripts are **analyzed but never executed**.

---

## 1. What the application does

SchemaSentinel is a SQL Server **migration preflight analyzer**. A user pastes a
migration script; the system analyzes it (without running it) and returns an
explainable risk report describing:

- what is changing,
- why it may be risky,
- which database objects are affected,
- what evidence was found,
- what to consider before deployment,
- whether the analysis used only the SQL text or also the target database.

Analysis is produced in **two layers**:

1. **Static analysis** — derived only from the submitted SQL text.
2. **Database-aware analysis** — enriched with read-only facts from the target
   database when the referenced object actually exists.

---

## 2. Technology stack

| Layer | Technology |
| --- | --- |
| Frontend | Angular 20 (standalone components), TypeScript, SCSS, Router, HttpClient, Reactive Forms |
| Backend | ASP.NET Core Web API, .NET 10, C#, EF Core (SQL Server), ScriptDom |
| Databases | SQL Server (LocalDB in development) |
| Tests | xUnit |

---

## 3. Solution structure

```
SchemaSentinel/
├── client/                      Angular application
│   └── src/app/
│       ├── core/                services + typed models
│       ├── features/            analyzer, history (+ detail)
│       └── shared/              severity-badge, analysis-report, sample data
├── server/                      ASP.NET Core Web API
│   ├── Api/                     Controllers + GlobalExceptionHandler
│   ├── Application/             Analysis orchestration, Rules, Interfaces
│   ├── Domain/                  Models, Enums, Parsing operation types
│   └── Infrastructure/          EF Core Data, Database analyzer, ScriptDom parser
├── tests/SchemaSentinel.Tests/  xUnit tests (parser, rules, service)
├── database/                    01-create / 02-schema / 03-seed SQL scripts
└── docs/                        documentation
```

---

## 4. The two databases

| Database | Role | Access |
| --- | --- | --- |
| **SchemaSentinelDb** | Application/history database; stores every analysis + findings | Read **and** write (EF Core) |
| **DemoTargetDb** | Sample "target" application database being analyzed | **Read-only** metadata/data checks |

Both run on `(localdb)\MSSQLLocalDB` in development. The submitted script is never
executed against either database.

### SchemaSentinelDb schema (EF Core)

- `Analysis` — `Id`, `CreatedAt`, `ScriptName`, `ScriptText`, `OverallSeverity`, `Summary`
- `AnalysisFinding` — `Id`, `AnalysisId`, `RuleId`, `Severity`, `Category`, `Title`,
  `Description`, `AffectedObject`, `Evidence`, `Recommendation`, `Source`

Defined in `server/Infrastructure/Data/Entities/` and mapped by
`SchemaSentinelDbContext`. Enums are stored as strings.

### DemoTargetDb schema (demo)

```
Customers ──< Orders ──< OrderItems
    │            └──< Payments
    └──< Addresses
```

Seeded (via `database/03-seed-demo-data.sql`) with deliberately risky data:
some `Customers.Email` values are NULL, some exceed 50 characters, and foreign
keys link the child tables — so database-aware findings can be demonstrated.

---

## 5. One-time setup (from scratch)

1. **Prerequisites:** .NET 10 SDK, Node.js, SQL Server LocalDB, `dotnet-ef` tool.
2. **Create the demo database + seed data:**
   ```powershell
   sqllocaldb start MSSQLLocalDB
   sqlcmd -S "(localdb)\MSSQLLocalDB" -b `
     -i database/01-create-databases.sql `
     -i database/02-create-demo-schema.sql `
     -i database/03-seed-demo-data.sql
   ```
3. **Create the history database (EF Core migration):**
   ```powershell
   dotnet ef database update --project server/server.csproj
   ```
   The API also auto-applies pending migrations on startup in **every** environment
   (see `app.Database.MigrateAsync()` in `server/Program.cs`), so a fresh deployment
   creates the schema automatically.
4. **Configure connection strings** in `server/appsettings.Development.json`
   (`SchemaSentinelDb`, `DemoTargetDb`). `appsettings.json` ships with empty,
   secret-free placeholders.
5. **Run the API:** `dotnet run --project server/server.csproj --launch-profile http`
   → `http://localhost:5083`.
6. **Run the client:** `cd client; npm install; npm start` → `http://localhost:4200`.

---

## 6. Runtime flow — the full request lifecycle

```mermaid
sequenceDiagram
    participant U as User
    participant NG as Angular (Analyzer)
    participant API as AnalysisController
    participant SVC as AnalysisService
    participant P as ScriptDomMigrationParser
    participant DB as SqlServerMetadataAnalyzer (DemoTargetDb)
    participant R as Risk Rules
    participant REC as RecommendationEngine
    participant H as AnalysisHistoryService (SchemaSentinelDb)

    U->>NG: Paste SQL + click "Analyze Migration"
    NG->>API: POST /api/analysis { script, scriptName }
    API->>SVC: AnalyzeAsync(request)
    SVC->>SVC: Validate (not empty, size limit)
    SVC->>P: Parse(script)  [STATIC, in-memory]
    P-->>SVC: ParsedMigration (operations, referenced objects, errors)
    SVC->>DB: IsAvailableAsync()
    DB-->>SVC: true / false
    loop each registered rule
        SVC->>R: EvaluateAsync(context)
        R->>DB: read-only checks (if available)
        DB-->>R: row counts, lengths, NULLs, dependencies
        R-->>SVC: findings (static and/or database-aware)
    end
    SVC->>REC: Build(findings)
    REC-->>SVC: recommendations
    SVC-->>API: AnalysisResult
    API->>H: SaveAsync(result, scriptText)
    H-->>API: (persisted; failure never breaks the response)
    API-->>NG: 200 OK + AnalysisResult (JSON)
    NG->>U: Render risk report
```

### Step-by-step

**Step 1 — Submit (frontend).**
`client/src/app/features/analyzer/analyzer.ts` holds a reactive form
(`scriptName` defaults to `migration.sql`, `script` is required). Sample chips
load canned scripts from `client/src/app/shared/data/sample-migrations.ts`.
`analyze()` blocks empty/duplicate submissions and calls the API service.

**Step 2 — HTTP call.**
`client/src/app/core/services/analysis.service.ts` POSTs `{ script, scriptName }`
to `${apiBaseUrl}/api/analysis` (base URL from `src/environments/environment.ts`).

**Step 3 — Controller.**
`server/Api/Controllers/AnalysisController.cs` validates the request, calls
`IAnalysisService.AnalyzeAsync`, then persists via `IAnalysisHistoryService`.
`MigrationValidationException` → HTTP 400 (safe `ProblemDetails`). Any unhandled
error is caught by `server/Api/GlobalExceptionHandler.cs` — no stack traces leak.

**Step 4 — Orchestration.**
`server/Application/Analysis/AnalysisService.cs` runs the pipeline:
1. Validate (non-empty, ≤ `MaxScriptLength` = 100,000 chars).
2. Parse the script (Layer 1).
3. Check database availability.
4. Build a `RuleEvaluationContext` (parsed migration + DB analyzer + availability flag).
5. Run **all** registered rules, collecting findings (a failing rule is logged and skipped).
6. Add a parse-warning finding if the script had syntax errors.
7. Resolve affected objects (existence check → Database Context counts).
8. Overall severity = highest finding severity.
9. Build summary + recommendations.

**Step 5 — Parsing (Layer 1, static).**
`server/Infrastructure/Parsing/ScriptDomMigrationParser.cs` uses `TSql160Parser`
to build an **AST** (the script is only tokenized/walked, never executed). A
visitor extracts typed operations from `server/Domain/Parsing/MigrationOperation.cs`:
`DropTableOperation`, `DropColumnOperation`, `AlterColumnOperation`,
`AddColumnOperation`, `ForeignKeyOperation`, `IndexOperation`. It also records the
referenced object names and any parse errors.

**Step 6 — Rules + database-aware analysis (Layer 2).**
Each rule in `server/Application/Rules/` implements `IMigrationRiskRule` and
self-selects the operations it cares about via `.OfType<T>()`. When the database
is available, rules call `IDatabaseMetadataAnalyzer` for read-only evidence.

**Step 7 — Recommendations.**
`server/Application/Analysis/RecommendationEngine.cs` produces a de-duplicated,
severity-ordered list, adding a general note when any finding is HIGH+.

**Step 8 — Persist history.**
`server/Infrastructure/Services/AnalysisHistoryService.cs` writes the `Analysis`
and child `AnalysisFinding` rows to SchemaSentinelDb. If saving fails, the result
is still returned.

**Step 9 — Render.**
`client/src/app/shared/components/analysis-report/analysis-report.ts` renders
Overall Risk, Summary, Database Context, Affected Objects, Findings (with evidence),
and Recommendations, using `severity-badge` for the colored indicators.

---

## 7. The risk rules

All text (titles, descriptions, evidence, recommendations) for both static and
database-aware findings lives **inside each rule file**.

| Rule (`server/Application/Rules/`) | Operation matched | Static severity | Database-aware upgrade |
| --- | --- | --- | --- |
| `DropTableRiskRule` | `DropTableOperation` | HIGH | adds row count + existence |
| `DropColumnRiskRule` | `DropColumnOperation` | HIGH | confirms column + row count |
| `ColumnNarrowingRiskRule` | `AlterColumnOperation` (shorter length) | MEDIUM | HIGH if rows exceed new length |
| `NullabilityChangeRiskRule` | `AlterColumnOperation` (→ NOT NULL) | MEDIUM | HIGH if existing NULLs |
| `RequiredColumnRiskRule` | `AddColumnOperation` (NOT NULL, no default) | MEDIUM | HIGH if table is populated |
| `DependencyRiskRule` | drop/alter targets + FK definitions | LOW/MEDIUM | lists real FK dependents |
| `IndexOperationRiskRule` | `IndexOperation` | LOW | MEDIUM on large tables |

### Static vs database-aware inside a rule (example)

`ColumnNarrowingRiskRule` builds its evidence from whichever layer applies:

```csharp
if (found && currentLength is not null)
{
    // DATABASE-AWARE: real numbers from DemoTargetDb
    evidence = $"Current length: {currentLength}\nNew length: {newLength}" +
        (exceeding is not null ? $"\nRows exceeding {newLength} characters: {exceeding:N0}" : "");
}
else
{
    // STATIC: derived only from the parsed SQL
    evidence = $"Statement reduces '{op.ColumnName}' to {op.DataType}({newLength}).";
}

Source = checkedDb && found ? AnalysisSource.DatabaseAnalysis : AnalysisSource.StaticAnalysis;
```

### Who decides which rule handles which script?

There is **no central router**:
- `server/Program.cs` decides which rules exist (one `AddScoped<IMigrationRiskRule, …>` per rule).
- `AnalysisService` runs **all** of them over the same parsed migration.
- Each rule claims its operations via `.OfType<T>()`; non-matching rules return nothing.

Adding a rule = create a class implementing `IMigrationRiskRule` + register one line.
No existing rule changes.

---

## 8. Database-aware safety model

`server/Infrastructure/Database/SqlServerMetadataAnalyzer.cs` performs all target
checks safely:

- Object/column names are **resolved from system catalog views** (`sys.tables`,
  `sys.columns`, …) using **parameters**.
- Any dynamic count query is built with **`QUOTENAME`** on catalog-verified names
  and executed via `sp_executesql` — submitted SQL can never become a command.
- Row counts use `sys.dm_db_partition_stats` (no table scan).
- Dependencies come from `sys.foreign_keys` joins.
- All queries are read-only; the migration is never executed.

---

## 9. Domain model reference

- **Enums** (`server/Domain/Enums/`): `RiskSeverity` (Info→Critical), `RiskCategory`
  (Destructive, DataLoss, Constraint, Compatibility, Dependency, Performance,
  Information), `AnalysisSource` (StaticAnalysis, DatabaseAnalysis).
- **`RiskFinding`**: ruleId, title, severity, category, description, affectedObject,
  evidence, recommendation, source, databaseChecked, databaseObjectFound.
- **`AnalysisResult`**: analysisId, overallSeverity, summary, findings,
  affectedObjects, databaseContext (checked / objectsFound / objectsNotFound),
  recommendations, createdAt, scriptName.

---

## 10. API endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/analysis` | Analyze a script and save it to history |
| GET | `/api/analysis?take=25` | Recent analysis history |
| GET | `/api/analysis/{id}` | A previously saved analysis |
| DELETE | `/api/analysis/{id}` | Delete a saved analysis |

Requests are JSON; enums serialize as strings; errors return `ProblemDetails`.

---

## 11. Frontend routing & screens

- `/` → **Analyzer** (`features/analyzer`) — editor, samples, run, report.
- `/history` → **History** (`features/history/history.ts`) — recent list, delete.
- `/history/:id` → **History detail** (`features/history/history-detail.ts`) —
  re-renders a saved report (route param bound via `withComponentInputBinding`).

Shared building blocks: `shared/components/analysis-report` (the full report) and
`shared/components/severity-badge` (colored INFO→CRITICAL indicator).

---

## 12. Error handling & validation

- **Empty / oversized script** → 400 with a clear message.
- **Malformed SQL** → parsing continues on what is understood; a LOW
  "could not be fully parsed" finding (category *Compatibility*) is added with the
  ScriptDom error location.
- **Object not in DemoTargetDb** → static analysis still runs; the finding notes
  that database-specific validation was not possible.
- **Database unavailable** → analysis degrades to static-only; Database Context shows
  "Static analysis only".
- **Unhandled server error** → `GlobalExceptionHandler` returns a safe `ProblemDetails`.
- **Frontend** → loading state, duplicate-submit guard, friendly messages for
  unreachable API / invalid SQL.

---

## 13. Testing

`tests/SchemaSentinel.Tests/` (xUnit) covers:
- the ScriptDom parser (each operation type, referenced objects, malformed, empty),
- every rule in both static and database-aware modes (using an in-memory
  `FakeDatabaseMetadataAnalyzer`),
- the `AnalysisService` (empty → validation error, malformed → parse warning,
  severity aggregation, database-context counts).

```powershell
dotnet test tests/SchemaSentinel.Tests/SchemaSentinel.Tests.csproj
```

No destructive migrations are executed during tests.

---

## 14. End-to-end example

Input (`scriptName = narrow.sql`):

```sql
ALTER TABLE Customers
ALTER COLUMN Email VARCHAR(50) NOT NULL;
```

Against the seeded DemoTargetDb this produces:

- **COLUMN_NARROWING** — HIGH — `Current length: 100 / New length: 50 / Rows exceeding 50 characters: 3`
- **NULLABILITY_CHANGE** — HIGH — `Existing NULL values: 4`
- **DEPENDENCY_IMPACT** — MEDIUM — `Addresses.CustomerId → Customers`, `Orders.CustomerId → Customers`
- **Overall severity:** HIGH; **Source:** Static + Database Analysis; saved to history.

For a table that does **not** exist (e.g. `EmployeeRecords`), the same operations
are still flagged statically, with a note that database validation was unavailable.

---

## 15. Build & run quick reference

```powershell
# Backend
dotnet build SchemaSentinel.slnx
dotnet run --project server/server.csproj --launch-profile http   # http://localhost:5083

# Frontend
cd client
npm start                                                         # http://localhost:4200
npx ng build --configuration production                           # production bundle

# Tests
dotnet test tests/SchemaSentinel.Tests/SchemaSentinel.Tests.csproj
```

---

## 16. Deployment (cloud)

Local development uses LocalDB, but the application is designed to run in a hosted
environment (e.g. **Render**) against a deployed SQL Server database.

### Configuration via environment

- **Connection strings** are read by `builder.Configuration.GetConnectionString(...)`
  for `SchemaSentinelDb` and `DemoTargetDb`. In production these are supplied as
  environment variables (e.g. `ConnectionStrings__SchemaSentinelDb`,
  `ConnectionStrings__DemoTargetDb`) rather than in `appsettings.json`, which ships
  with empty, secret-free placeholders. No secrets are committed.
- **CORS** allowed origins come from the `Cors:AllowedOrigins` array in
  `server/appsettings.json`, which includes both the local client
  (`http://localhost:4200`) and the deployed client origin
  (`https://schemasentinel-client.onrender.com`). Add your own deployed frontend
  origin here; unrestricted CORS is never used.

### Schema creation on deploy

`server/Program.cs` applies EF Core migrations on startup in **every** environment:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SchemaSentinelDbContext>();
    await db.Database.MigrateAsync();
}
```

So a fresh deployment provisions the `SchemaSentinelDb` history schema automatically;
`DemoTargetDb` is created/seeded separately using the scripts in `database/`.

### Resilience

The `SchemaSentinelDbContext` is registered with transient-fault retries, which is
important for cloud SQL where brief connection drops are normal:

```csharp
options.UseSqlServer(connectionString, sql =>
    sql.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorNumbersToAdd: null));
```

### Deployment checklist

1. Provision a SQL Server instance and two databases (or two logical catalogs).
2. Set `ConnectionStrings__SchemaSentinelDb` and `ConnectionStrings__DemoTargetDb`
   as environment variables on the API host.
3. Add the deployed frontend origin to `Cors:AllowedOrigins`.
4. Deploy the API (migrations run automatically on first boot).
5. Run `database/02-create-demo-schema.sql` + `03-seed-demo-data.sql` against the
   deployed `DemoTargetDb`.
6. Build the client with `npx ng build --configuration production` and host the
   `dist/` output; ensure its `environment.production.ts` `apiBaseUrl` points at the
   deployed API (empty string if served from the same origin).

> The public demo should contain only fictional sample data, and the target
> database account should have the minimum permissions needed for read-only
> metadata/data analysis.

---

## 17. Positioning

SchemaSentinel is an **explainable migration preflight analyzer** that highlights
potentially risky schema, data and dependency changes before deployment. It is
**not** a replacement for SQL Server tooling, a complete migration validator, or a
guaranteed safe-deployment system. Static and database-aware analysis are always
clearly distinguished in the report.
