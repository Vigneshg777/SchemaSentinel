/*
    01-create-databases.sql
    Creates the two logical databases used by SchemaSentinel on LocalDB.

    - SchemaSentinelDb : application/history database (schema managed by EF Core migrations).
    - DemoTargetDb     : sample target database used ONLY for read-only contextual analysis.

    Safe to run repeatedly. Submitted migration scripts are NEVER executed here.
*/

IF DB_ID(N'SchemaSentinelDb') IS NULL
BEGIN
    CREATE DATABASE [SchemaSentinelDb];
END;
GO

IF DB_ID(N'DemoTargetDb') IS NULL
BEGIN
    CREATE DATABASE [DemoTargetDb];
END;
GO
