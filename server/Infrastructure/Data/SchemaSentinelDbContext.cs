using Microsoft.EntityFrameworkCore;
using SchemaSentinel.Infrastructure.Data.Entities;

namespace SchemaSentinel.Infrastructure.Data;

/// <summary>Application/history database context for SchemaSentinelDb.</summary>
public sealed class SchemaSentinelDbContext(DbContextOptions<SchemaSentinelDbContext> options)
    : DbContext(options)
{
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<AnalysisFinding> AnalysisFindings => Set<AnalysisFinding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.ScriptName).HasMaxLength(256);
            entity.Property(a => a.Summary).HasMaxLength(2000);
            entity.Property(a => a.OverallSeverity).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.ScriptText);
            entity.HasIndex(a => a.CreatedAt);

            entity.HasMany(a => a.Findings)
                .WithOne(f => f.Analysis)
                .HasForeignKey(f => f.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnalysisFinding>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.RuleId).HasMaxLength(64);
            entity.Property(f => f.Title).HasMaxLength(256);
            entity.Property(f => f.AffectedObject).HasMaxLength(512);
            entity.Property(f => f.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(f => f.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(f => f.Source).HasConversion<string>().HasMaxLength(32);
        });
    }
}
