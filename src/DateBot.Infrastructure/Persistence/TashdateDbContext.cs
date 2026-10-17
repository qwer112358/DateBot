using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DateBot.Infrastructure.Persistence;

public sealed class TashdateDbContext(DbContextOptions<TashdateDbContext> options) : DbContext(options)
{
    public DbSet<MatchRecord> Matches => Set<MatchRecord>();

    public DbSet<ActionRecord> Actions => Set<ActionRecord>();

    public DbSet<InboxRecord> Inbox => Set<InboxRecord>();

    public DbSet<SyncStateRecord> SyncState => Set<SyncStateRecord>();

    /// <summary>SQLite cannot compare DateTimeOffset stored as text; binary ticks keep range queries in SQL.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MatchRecord>(e =>
        {
            e.ToTable("Profiles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.BotMessageId).IsUnique();
            e.HasIndex(x => x.PersonId).IsUnique();
            e.HasIndex(x => x.ReceivedAt);
            e.Property(x => x.PersonId).HasMaxLength(128);
            e.Property(x => x.Name).HasMaxLength(64);
            e.Property(x => x.City).HasMaxLength(64);
            e.Property(x => x.Username).HasMaxLength(32);
            e.Property(x => x.PhotoFileId).HasMaxLength(256);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasMany(x => x.Actions).WithOne().HasForeignKey(a => a.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActionRecord>(e =>
        {
            e.ToTable("Actions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProfileId, x.Sequence }).IsUnique();
            e.HasIndex(x => x.UpdatedAt);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<InboxRecord>(e =>
        {
            e.ToTable("BotMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.SentAt);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<SyncStateRecord>(e =>
        {
            e.ToTable("SyncState");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(64);
        });
    }
}

/// <summary>Used by <c>dotnet ef migrations</c> only.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TashdateDbContext>
{
    public TashdateDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TashdateDbContext>().UseSqlite("Data Source=design.db").Options);
}
