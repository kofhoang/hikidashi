using Hikidashi.Core.Facts;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static LanguageExt.Prelude;

namespace Hikidashi.Data;

/// <summary>
/// EF Core adapter for <see cref="IFactRepository"/>. CRUD/list use LINQ; the forgiving search and
/// the keyword-count aggregate use raw SQL via <c>FromSql</c> (the LINQ provider can't express
/// per-term ILIKE ranking or <c>unnest</c>). Reads are no-tracking — the repo hands back detached
/// domain objects. Search is forgiving: per-term ILIKE over (keywords + content), AND/OR by match
/// mode, ranked by how many terms hit, then recency.
/// </summary>
public sealed class FactRepository(FactsDbContext db) : IFactRepository
{
    public async Task<FactId> AddAsync(Fact fact)
    {
        db.Facts.Add(ToRecord(fact));
        await db.SaveChangesAsync();
        return fact.Id;
    }

    public async Task<Option<Fact>> FindByIdAsync(FactId id)
    {
        var record = await db.Facts.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id.Value);
        return record is null ? None : Some(ToDomain(record));
    }

    public async Task<Seq<Fact>> SearchAsync(Seq<string> terms, MatchMode match, int limit)
    {
        var termList = terms.ToArray();
        if (termList.Length == 0)
            return LanguageExt.Seq<Fact>.Empty;

        var conds = new List<string>(termList.Length);
        var scores = new List<string>(termList.Length);
        var ps = new List<NpgsqlParameter>(termList.Length + 1);
        for (var i = 0; i < termList.Length; i++)
        {
            conds.Add($"haystack ILIKE @p{i}");
            scores.Add($"(CASE WHEN haystack ILIKE @p{i} THEN 1 ELSE 0 END)");
            ps.Add(new NpgsqlParameter($"p{i}", "%" + termList[i] + "%"));
        }
        ps.Add(new NpgsqlParameter("lim", limit));

        var joiner = match is MatchMode.All ? " AND " : " OR ";
        var sql =
            "SELECT id, content, keywords, enriched, metadata, created_at, updated_at FROM ("
            + "  SELECT *, (array_to_string(keywords, ' ') || ' ' || content) AS haystack FROM facts"
            + ") h "
            + $"WHERE {string.Join(joiner, conds)} "
            + $"ORDER BY ({string.Join(" + ", scores)}) DESC, updated_at DESC LIMIT @lim";

        var rows = await db.Facts.FromSqlRaw(sql, [.. ps]).AsNoTracking().ToListAsync();
        return toSeq(rows.Select(ToDomain));
    }

    public async Task<Seq<Fact>> ListAsync(int limit, int offset)
    {
        var rows = await db
            .Facts.AsNoTracking()
            .OrderByDescending(f => f.UpdatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
        return toSeq(rows.Select(ToDomain));
    }

    public async Task<Seq<Fact>> ListUnenrichedAsync(int limit)
    {
        var rows = await db
            .Facts.AsNoTracking()
            .Where(f => !f.Enriched)
            .OrderByDescending(f => f.UpdatedAt)
            .Take(limit)
            .ToListAsync();
        return toSeq(rows.Select(ToDomain));
    }

    public async Task<Seq<KeywordCount>> ListKeywordsAsync(Option<string> prefix)
    {
        var pfx = prefix.IfNone(string.Empty);
        const string sql =
            "SELECT kw AS \"Keyword\", count(*)::int AS \"Count\" "
            + "FROM facts f, unnest(f.keywords) AS kw "
            + "WHERE (@pfx = '' OR kw ILIKE @pfx || '%') "
            + "GROUP BY kw ORDER BY count(*) DESC, kw ASC";

        var rows = await db
            .KeywordCounts.FromSqlRaw(sql, new NpgsqlParameter("pfx", pfx))
            .AsNoTracking()
            .ToListAsync();
        return toSeq(rows.Select(r => new KeywordCount(r.Keyword, r.Count)));
    }

    public async Task<bool> UpdateAsync(Fact fact)
    {
        var rows = await db
            .Facts.Where(f => f.Id == fact.Id.Value)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(f => f.Content, fact.Content)
                    .SetProperty(f => f.Keywords, fact.Keywords.ToArray())
                    .SetProperty(f => f.Enriched, fact.Enriched)
                    .SetProperty(f => f.Metadata, fact.Metadata)
                    .SetProperty(f => f.UpdatedAt, fact.UpdatedAt)
            );
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(FactId id)
    {
        var rows = await db.Facts.Where(f => f.Id == id.Value).ExecuteDeleteAsync();
        return rows > 0;
    }

    private static Fact ToDomain(FactRecord r) =>
        new(
            new FactId(r.Id),
            r.Content,
            toSeq(r.Keywords),
            r.Enriched,
            r.Metadata,
            r.CreatedAt,
            r.UpdatedAt
        );

    private static FactRecord ToRecord(Fact f) =>
        new()
        {
            Id = f.Id.Value,
            Content = f.Content,
            Keywords = f.Keywords.ToArray(),
            Enriched = f.Enriched,
            Metadata = f.Metadata,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt,
        };
}
