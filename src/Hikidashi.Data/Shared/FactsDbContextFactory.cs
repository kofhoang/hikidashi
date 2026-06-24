using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hikidashi.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the model without a running host.
/// The placeholder connection string is never used at runtime.
/// </summary>
public sealed class FactsDbContextFactory : IDesignTimeDbContextFactory<FactsDbContext>
{
    public FactsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FactsDbContext>()
            .UseNpgsql("Host=localhost;Database=hikidashi;Username=postgres;Password=postgres")
            .Options;
        return new FactsDbContext(options);
    }
}
