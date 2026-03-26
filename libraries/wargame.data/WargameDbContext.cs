using Microsoft.EntityFrameworkCore;
using WargameData.Entities;

namespace WargameData;

/// <summary>
/// Entity Framework Core database context for the War Game Visualizer application.
/// Provides access to the <see cref="Scenarios"/> table backed by Azure SQL.
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

    /// <summary>The Scenarios table.</summary>
    public DbSet<ScenarioEntity> Scenarios { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScenarioEntity>(entity =>
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

            // BoundingBox is an owned entity – its columns are stored in the
            // Scenarios table with an "BoundingBox_" column-name prefix.
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
