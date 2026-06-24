using Dapper;
using Hikidashi.Core.Facts;
using LanguageExt;
using Npgsql;
using NpgsqlTypes;
using static LanguageExt.Prelude;

namespace Hikidashi.Data;

/// <summary>
/// Postgres adapter for <see cref="IFactRepository"/>. Reads go through Dapper; writes and the
/// array-parameter search use raw Npgsql so <c>text[]</c> binds correctly (Dapper would otherwise
/// expand an array parameter into a positional list). Search is forgiving: per-term ILIKE over
/// (keywords + content), AND/OR by match mode, ranked by how many terms hit, then recency.
/// </summary>
public sealed class FactRepository(NpgsqlDataSource dataSource) : IFactRepository
{
    private const string Cols =
        "id AS \"Id\", content AS \"Content\", keywords AS \"Keywords\", enriched AS \"Enriched\", "
        + "metadata::text AS \"Metadata\", created_at AS \"CreatedAt\", updated_at AS \"UpdatedAt\"";

    public async Task<FactId> AddAsync(Fact fact)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO facts (id, content, keywords, enriched, metadata, created_at, updated_at) "
            + "VALUES (@id, @content, @keywords, @enriched, @metadata::jsonb, @created_at, @updated_at)";
        AddWriteParams(cmd, fact);
        await cmd.ExecuteNonQueryAsync();
        return fact.Id;
    }

    public async Task<Option<Fact>> FindByIdAsync(FactId id)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var row = await conn.QuerySingleOrDefaultAsync<FactRow>(
            $"SELECT {Cols} FROM facts WHERE id = @id",
            new { id = id.Value }
        );
        return row is null ? None : Some(row.ToFact());
    }

    public async Task<Seq<Fact>> SearchAsync(Seq<string> terms, MatchMode match, int limit)
    {
        var termList = terms.ToArray();
        if (termList.Length == 0)
            return LanguageExt.Seq<Fact>.Empty;

        var conds = new List<string>(termList.Length);
        var scores = new List<string>(termList.Length);
        var p = new DynamicParameters();
        for (var i = 0; i < termList.Length; i++)
        {
            p.Add($"t{i}", "%" + termList[i] + "%");
            conds.Add($"haystack ILIKE @t{i}");
            scores.Add($"(CASE WHEN haystack ILIKE @t{i} THEN 1 ELSE 0 END)");
        }
        p.Add("lim", limit);

        var joiner = match == MatchMode.All ? " AND " : " OR ";
        var sql =
            "WITH h AS (SELECT *, (array_to_string(keywords, ' ') || ' ' || content) AS haystack FROM facts) "
            + $"SELECT {Cols} FROM h WHERE {string.Join(joiner, conds)} "
            + $"ORDER BY ({string.Join(" + ", scores)}) DESC, updated_at DESC LIMIT @lim";

        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.QueryAsync<FactRow>(sql, p);
        return toSeq(rows.Select(r => r.ToFact()));
    }

    public async Task<Seq<Fact>> ListAsync(int limit, int offset)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.QueryAsync<FactRow>(
            $"SELECT {Cols} FROM facts ORDER BY updated_at DESC LIMIT @limit OFFSET @offset",
            new { limit, offset }
        );
        return toSeq(rows.Select(r => r.ToFact()));
    }

    public async Task<Seq<Fact>> ListUnenrichedAsync(int limit)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.QueryAsync<FactRow>(
            $"SELECT {Cols} FROM facts WHERE enriched = false ORDER BY updated_at DESC LIMIT @limit",
            new { limit }
        );
        return toSeq(rows.Select(r => r.ToFact()));
    }

    public async Task<Seq<KeywordCount>> ListKeywordsAsync(Option<string> prefix)
    {
        var pfx = prefix.IfNone(string.Empty);
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.QueryAsync<KeywordCount>(
            "SELECT kw AS \"Keyword\", count(*)::int AS \"Count\" "
                + "FROM facts f, unnest(f.keywords) AS kw "
                + "WHERE (@pfx = '' OR kw ILIKE @pfx || '%') "
                + "GROUP BY kw ORDER BY count(*) DESC, kw ASC",
            new { pfx }
        );
        return toSeq(rows);
    }

    public async Task<bool> UpdateAsync(Fact fact)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE facts SET content = @content, keywords = @keywords, enriched = @enriched, "
            + "metadata = @metadata::jsonb, updated_at = @updated_at WHERE id = @id";
        AddWriteParams(cmd, fact);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(FactId id)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM facts WHERE id = @id",
            new { id = id.Value }
        );
        return rows > 0;
    }

    private static void AddWriteParams(NpgsqlCommand cmd, Fact fact)
    {
        cmd.Parameters.AddWithValue("id", fact.Id.Value);
        cmd.Parameters.AddWithValue("content", fact.Content);
        cmd.Parameters.Add(
            new NpgsqlParameter("keywords", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = fact.Keywords.ToArray(),
            }
        );
        cmd.Parameters.AddWithValue("enriched", fact.Enriched);
        cmd.Parameters.AddWithValue("metadata", fact.Metadata);
        cmd.Parameters.AddWithValue("created_at", fact.CreatedAt);
        cmd.Parameters.AddWithValue("updated_at", fact.UpdatedAt);
    }

    private sealed class FactRow
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = "";
        public string[] Keywords { get; init; } = [];
        public bool Enriched { get; init; }
        public string Metadata { get; init; } = "{}";
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public Fact ToFact() =>
            new(
                new FactId(Id),
                Content,
                toSeq(Keywords),
                Enriched,
                Metadata,
                new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)),
                new DateTimeOffset(DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc))
            );
    }
}
