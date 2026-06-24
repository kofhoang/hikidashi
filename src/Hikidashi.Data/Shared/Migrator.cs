using System.Reflection;
using Npgsql;

namespace Hikidashi.Data;

/// <summary>
/// Minimal forward-only migration runner. Applies the embedded <c>Migrations/*.sql</c> files in
/// filename order inside a transaction each, tracking applied ones in <c>schema_migrations</c>.
/// Deliberately tiny — no EF, no external migration tool.
/// </summary>
public static class Migrator
{
    public static async Task ApplyAsync(NpgsqlDataSource dataSource, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        await using (var ensure = conn.CreateCommand())
        {
            ensure.CommandText =
                "CREATE TABLE IF NOT EXISTS schema_migrations ("
                + "filename text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())";
            await ensure.ExecuteNonQueryAsync(ct);
        }

        var asm = typeof(Migrator).Assembly;
        var resources = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".Migrations.") && n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var res in resources)
        {
            var parts = res.Split('.');
            var name = parts[^2]; // e.g. "0001_init"

            await using (var check = conn.CreateCommand())
            {
                check.CommandText = "SELECT 1 FROM schema_migrations WHERE filename = @f";
                check.Parameters.AddWithValue("f", name);
                if (await check.ExecuteScalarAsync(ct) is not null)
                    continue;
            }

            var sql = await ReadResource(asm, res);

            await using var tx = await conn.BeginTransactionAsync(ct);
            await using (var run = conn.CreateCommand())
            {
                run.Transaction = tx;
                run.CommandText = sql;
                await run.ExecuteNonQueryAsync(ct);
            }
            await using (var mark = conn.CreateCommand())
            {
                mark.Transaction = tx;
                mark.CommandText = "INSERT INTO schema_migrations (filename) VALUES (@f)";
                mark.Parameters.AddWithValue("f", name);
                await mark.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
    }

    private static async Task<string> ReadResource(Assembly asm, string resource)
    {
        await using var stream =
            asm.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded migration: {resource}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
