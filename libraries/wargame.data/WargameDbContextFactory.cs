using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WargameData;

/// <summary>
/// Design-time factory for <see cref="WargameDbContext"/>.
/// Used by the EF Core tooling (<c>dotnet ef migrations add</c>) when no
/// startup project is available.
/// </summary>
public class WargameDbContextFactory : IDesignTimeDbContextFactory<WargameDbContext>
{
    /// <inheritdoc />
    public WargameDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WargameDbContext>();

        // Use a placeholder connection string for design-time tooling.
        // At runtime the real connection string is injected via configuration.
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=wargame;User Id=sa;Password=placeholder;TrustServerCertificate=True;");

        return new WargameDbContext(optionsBuilder.Options);
    }
}
