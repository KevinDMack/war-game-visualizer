using Microsoft.EntityFrameworkCore;
using WargameVisualizer.Protos;

namespace WargameData;

/// <summary>
/// Entity Framework Core database context for the War Game Visualizer application.
/// Uses the proto-generated <see cref="Scenario"/> and <see cref="BoundingBox"/> types
/// directly as EF entity models, eliminating any duplication between the proto
/// definitions and the database schema.
/// </summary>
public class WargameDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="WargameDbContext"/> with the
    /// given options (connection string, provider, etc.).
    /// </summary>
    public WargameDbContext(DbContextOptions<WargameDbContext> options)
        : base(options)
    {
    }

    /// <summary>The Scenarios table, typed directly to the proto-generated <see cref="Scenario"/> class.</summary>
    public DbSet<Scenario> Scenarios { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.ToTable("Scenarios");

            entity.HasKey(s => s.ScenarioId);

            entity.Property(s => s.ScenarioId)
                  .HasMaxLength(36)
                  .IsRequired();

            entity.Property(s => s.ScenarioName)
                  .HasMaxLength(256)
                  .IsRequired();

            entity.Property(s => s.Summary)
                  .HasMaxLength(2048)
                  .IsRequired();

            // BoundingBox is an owned entity – its columns are stored inline in the
            // Scenarios table with a "BoundingBox_" column-name prefix.
            // The proto-generated BoundingBox class is used directly; EF Core maps
            // its read-write scalar properties to columns via Fluent API.
            entity.OwnsOne(s => s.BoundingBox, bb =>
            {
                bb.Property(b => b.MinLatitude).HasColumnName("BoundingBox_MinLatitude");
                bb.Property(b => b.MinLongitude).HasColumnName("BoundingBox_MinLongitude");
                bb.Property(b => b.MaxLatitude).HasColumnName("BoundingBox_MaxLatitude");
                bb.Property(b => b.MaxLongitude).HasColumnName("BoundingBox_MaxLongitude");
            });
        });
    }
}
