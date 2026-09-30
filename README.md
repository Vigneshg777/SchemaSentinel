# SchemaSentinel

An explainable SQL Server migration preflight analyzer that identifies
potentially risky schema, data, and dependency changes **before** deployment.

SchemaSentinel lets you paste a SQL Server migration script and analyzes it
without executing it. It combines **static SQL analysis** with optional
**database-aware analysis** against a target database, then produces an
explainable risk report: what is changing, why it may be risky, which objects
are affected, the supporting evidence, and what to consider before deploying.

> ⚠️ Submitted migration scripts are analyzed but **never executed**.

---

## Features

- SQL Server migration parsing with `Microsoft.SqlServer.TransactSql.ScriptDom`
- Static risk analysis that works even when referenced objects do not exist
- Database-aware analysis using safe, read-only, parameterized metadata queries
- Destructive change detection (DROP TABLE / DROP COLUMN)
- Column narrowing and truncation detection with affected row counts
- Nullability (NULL → NOT NULL) and required-column checks
- Foreign key / dependency impact analysis
- Index / expensive operation warnings
- Explainable severity model (INFO / LOW / MEDIUM / HIGH / CRITICAL)
- Actionable recommendations
- Analysis history persisted to SchemaSentinelDb

---

## Architecture

```
Angular 20
    |
    | HTTP/JSON
    v
ASP.NET Core .NET 10 Web API
    |
    +-- Migration Parser        (ScriptDom AST)
    +-- Risk Rule Engine        (independent IMigrationRiskRule rules)
    +-- Schema Metadata Analyzer (read-only, parameterized)
    +-- Dependency Analyzer
    +-- Recommendation Engine
    +-- Analysis History Service
    |
    v
SQL Server
    |
    +-- SchemaSentinelDb   (application/history)
    +-- DemoTargetDb       (read-only sample target database)
```

Analysis has two layers that are clearly distinguished in the UI:

1. **Static analysis** — based only on the submitted SQL.
2. **Database-aware analysis** — based on the actual target schema/data when the
   referenced object exists.

---

## Technology

- **Frontend:** Angular 20 (standalone components), TypeScript, SCSS, Angular Router, HttpClient, Reactive Forms
- **Backend:** ASP.NET Core Web API, .NET 10, C#, Entity Framework Core (SQL Server), ScriptDom
- **Database:** SQL Server (LocalDB for development)

---

## Project structure

```
SchemaSentinel/
├── client/        Angular application
├── server/        ASP.NET Core Web API
│   ├── Api/            Controllers, global exception handling
│   ├── Application/    Analysis orchestration, rules, interfaces
│   ├── Domain/         Models, enums, parsing operation types
│   └── Infrastructure/ EF Core data, database analyzer, ScriptDom parser
├── tests/         xUnit unit tests
├── database/      SQL setup scripts (create / schema / seed)
└── README.md
```

---

## Local setup

### 1. Prerequisites

- .NET 10 SDK
- Node.js (use the version already configured for the Angular workspace)
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`)
- EF Core CLI: `dotnet tool install --global dotnet-ef`

### 2. Clone the repository

```powershell
git clone <your-fork-url> SchemaSentinel
cd SchemaSentinel
```

### 3. Configure LocalDB and create the databases

Run the database scripts in order (creates `DemoTargetDb` and seeds demo data):

```powershell
sqllocaldb start MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -b `
  -i database/01-create-databases.sql `
  -i database/02-create-demo-schema.sql `
  -i database/03-seed-demo-data.sql
```

### 4. Configure connection strings

Connection strings live in `server/appsettings.Development.json`:

```json
"ConnectionStrings": {
  "SchemaSentinelDb": "Server=(localdb)\\MSSQLLocalDB;Database=SchemaSentinelDb;Trusted_Connection=True;TrustServerCertificate=True",
  "DemoTargetDb":     "Server=(localdb)\\MSSQLLocalDB;Database=DemoTargetDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

No secrets are committed. `appsettings.json` ships with empty placeholders.

### 5. Create the history database (EF Core)

```powershell
dotnet ef database update --project server/server.csproj
```

The API also applies pending migrations automatically on startup in Development.

### 6. Run the API

```powershell
dotnet run --project server/server.csproj --launch-profile http
# API: http://localhost:5083
```

### 7. Run the Angular client

```powershell
cd client
npm install
npm start
# App: http://localhost:4200
```

### 8. Open the browser

Navigate to <http://localhost:4200>, paste a migration, and click **Analyze Migration**.

---

## Demo

The analyzer ships with built-in sample migrations you can load with one click:

| Sample | What it demonstrates |
| --- | --- |
| `DROP TABLE Customers;` | Destructive change + dependency impact |
| `ALTER TABLE Customers ALTER COLUMN Email VARCHAR(50) NOT NULL;` | Column narrowing (truncation) + NULL → NOT NULL |
| `ALTER TABLE Orders ADD PaymentReference VARCHAR(100) NOT NULL;` | Required column on a populated table |
| `CREATE INDEX IX_Orders_Status ON Orders (Status);` | Potentially expensive operation |
| `ALTER TABLE Customers ADD LoyaltyCode VARCHAR(20) NULL;` | Safe, low-risk change |

The seeded `DemoTargetDb` intentionally contains NULL emails, oversized email
values (> 50 chars) and foreign key relationships so database-aware findings can
be demonstrated immediately.

---

## Running the tests

```powershell
dotnet test tests/SchemaSentinel.Tests/SchemaSentinel.Tests.csproj
```

The test suite covers the parser and every initial risk rule in both static and
database-aware modes, plus malformed and empty SQL handling. No destructive
migrations are executed during tests.

---

## Safety

- Submitted migration scripts are **analyzed but never executed**.
- The target database is only queried with predefined, read-only, parameterized
  metadata queries. Object/column names are resolved from the system catalog and
  quoted with `QUOTENAME` before any dynamic count query runs.
- A maximum script size is enforced and stack traces are never returned to clients.

---

## Future improvements

- Custom, user-authored rules
- GitHub Action / CI-CD integration
- SARIF output
- User-provided read-only database connections
- EF Core migration integration
- Schema comparison
- Docker Compose deployment (Angular + API + SQL Server)

SchemaSentinel is a preflight risk analyzer. It is **not** a replacement for SQL
Server tooling, a complete migration validator, or a guaranteed safe-deployment
system.
