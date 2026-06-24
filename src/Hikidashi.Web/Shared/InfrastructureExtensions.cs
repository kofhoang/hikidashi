using Hikidashi.Core;
using Hikidashi.Core.Facts;
using Hikidashi.Data;
using Npgsql;

namespace Hikidashi.Web;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string not configured (DATABASE_URL or ConnectionStrings:DefaultConnection)."
            );

        services
            .AddSingleton(NpgsqlDataSource.Create(connectionString))
            .AddSingleton<IClock>(SystemClock.Instance)
            .AddSingleton<IIdGenerator>(SystemIdGenerator.Instance)
            .AddScoped<IFactRepository, FactRepository>()
            .AddScoped<AppRuntime>();

        return services;
    }
}
