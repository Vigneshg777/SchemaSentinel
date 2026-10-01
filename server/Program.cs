using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SchemaSentinel.Api;
using SchemaSentinel.Application.Analysis;
using SchemaSentinel.Application.Interfaces;
using SchemaSentinel.Application.Rules;
using SchemaSentinel.Infrastructure.Data;
using SchemaSentinel.Infrastructure.Database;
using SchemaSentinel.Infrastructure.Parsing;
using SchemaSentinel.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDbContext<SchemaSentinelDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SchemaSentinelDb"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

// Parsing, database analysis and orchestration.
builder.Services.AddSingleton<IMigrationParser, ScriptDomMigrationParser>();
builder.Services.AddScoped<IDatabaseMetadataAnalyzer, SqlServerMetadataAnalyzer>();
builder.Services.AddSingleton<IRecommendationEngine, RecommendationEngine>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<IAnalysisHistoryService, AnalysisHistoryService>();

// Risk rules — add new rules here without touching existing ones.
builder.Services.AddScoped<IMigrationRiskRule, DropTableRiskRule>();
builder.Services.AddScoped<IMigrationRiskRule, DropColumnRiskRule>();
builder.Services.AddScoped<IMigrationRiskRule, ColumnNarrowingRiskRule>();
builder.Services.AddScoped<IMigrationRiskRule, NullabilityChangeRiskRule>();
builder.Services.AddScoped<IMigrationRiskRule, RequiredColumnRiskRule>();
builder.Services.AddScoped<IMigrationRiskRule, DependencyRiskRule>();
builder.Services.AddScoped<IMigrationRiskRule, IndexOperationRiskRule>();

const string corsPolicy = "SchemaSentinelClient";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
    options.AddPolicy(corsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
// Apply EF Core migrations in every environment.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SchemaSentinelDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors(corsPolicy);
app.MapControllers();

app.Run();

/// <summary>Exposed so the API can be referenced from the test project.</summary>
public partial class Program;
