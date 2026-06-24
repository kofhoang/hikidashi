using Microsoft.EntityFrameworkCore;

namespace Hikidashi.Data;

/// <summary>
/// EF Core context for the facts store. Columns are mapped to the existing snake_case names so the
/// raw-SQL queries (search, keyword counts) and the LINQ queries share one schema. The keyword-count
/// projection is keyless and exists only for <c>FromSql</c> results.
/// </summary>
public sealed class FactsDbContext(DbContextOptions<FactsDbContext> options) : DbContext(options)
{
    public DbSet<FactRecord> Facts => Set<FactRecord>();
    public DbSet<KeywordCountRecord> KeywordCounts => Set<KeywordCountRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var fact = modelBuilder.Entity<FactRecord>();
        fact.ToTable("facts");
        fact.HasKey(f => f.Id);
        fact.Property(f => f.Id).HasColumnName("id");
        fact.Property(f => f.Content).HasColumnName("content");
        fact.Property(f => f.Keywords).HasColumnName("keywords");
        fact.Property(f => f.Enriched).HasColumnName("enriched").HasDefaultValue(false);
        fact.Property(f => f.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");
        fact.Property(f => f.CreatedAt).HasColumnName("created_at");
        fact.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        // Array membership/overlap; substring search is plain ILIKE (unindexed, fine at this scale).
        fact.HasIndex(f => f.Keywords).HasMethod("gin").HasDatabaseName("facts_keywords_gin");

        // Query-only projection for list_keywords; never mapped to a table.
        modelBuilder.Entity<KeywordCountRecord>().HasNoKey().ToView(null);
    }
}
