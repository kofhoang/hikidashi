using Hikidashi.Data;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Hikidashi.Data.Tests;

/// <summary>
/// Spins up a real Postgres via Testcontainers and runs the migrations. If Docker is unavailable
/// the fixture stays <see cref="Available"/> = false and the tests skip rather than fail.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public NpgsqlDataSource? DataSource { get; private set; }
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            DataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
            await Migrator.ApplyAsync(DataSource);
            Available = true;
        }
        catch
        {
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null)
            await DataSource.DisposeAsync();
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
