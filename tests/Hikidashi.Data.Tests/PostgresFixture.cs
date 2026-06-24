using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Hikidashi.Data.Tests;

/// <summary>
/// Spins up a real Postgres via Testcontainers and applies EF migrations. If Docker is unavailable
/// the fixture stays <see cref="Available"/> = false and the tests skip rather than fail.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private DbContextOptions<FactsDbContext>? _options;
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            _options = new DbContextOptionsBuilder<FactsDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;
            await using var ctx = new FactsDbContext(_options);
            await ctx.Database.MigrateAsync();
            Available = true;
        }
        catch
        {
            Available = false;
        }
    }

    public FactsDbContext NewContext() => new(_options!);

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
