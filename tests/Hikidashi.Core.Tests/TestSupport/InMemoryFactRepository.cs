using System.Linq;
using Hikidashi.Core.Facts;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Hikidashi.Core.Tests;

/// <summary>
/// In-memory <see cref="IFactRepository"/> that mirrors the intended search semantics
/// (substring over keywords + content, any/all, rank by term-hit-count then recency) so Core
/// handler logic can be tested without a database. The real Postgres query is tested separately.
/// </summary>
public sealed class InMemoryFactRepository : IFactRepository
{
    private readonly Dictionary<Guid, Fact> _store = new();

    public Task<FactId> AddAsync(Fact fact)
    {
        _store[fact.Id.Value] = fact;
        return Task.FromResult(fact.Id);
    }

    public Task<Option<Fact>> FindByIdAsync(FactId id) =>
        Task.FromResult(_store.TryGetValue(id.Value, out var f) ? Some(f) : Option<Fact>.None);

    public Task<Seq<Fact>> SearchAsync(Seq<string> terms, MatchMode match, int limit)
    {
        bool Hits(Fact f, string term) =>
            (string.Join(" ", f.Keywords) + " " + f.Content).Contains(
                term,
                StringComparison.OrdinalIgnoreCase
            );

        int Score(Fact f) => terms.Filter(t => Hits(f, t)).Count;

        bool Matches(Fact f) =>
            match == MatchMode.All ? terms.ForAll(t => Hits(f, t)) : terms.Exists(t => Hits(f, t));

        var results = _store
            .Values.Where(Matches)
            .OrderByDescending(Score)
            .ThenByDescending(f => f.UpdatedAt)
            .Take(limit);

        return Task.FromResult(toSeq(results));
    }

    public Task<Seq<Fact>> ListAsync(int limit, int offset) =>
        Task.FromResult(
            toSeq(_store.Values.OrderByDescending(f => f.UpdatedAt).Skip(offset).Take(limit))
        );

    public Task<Seq<Fact>> ListUnenrichedAsync(int limit) =>
        Task.FromResult(
            toSeq(
                _store
                    .Values.Where(f => !f.Enriched)
                    .OrderByDescending(f => f.UpdatedAt)
                    .Take(limit)
            )
        );

    public Task<Seq<KeywordCount>> ListKeywordsAsync(Option<string> prefix)
    {
        var p = prefix.IfNone(string.Empty);
        var counts = _store
            .Values.SelectMany(f => (IEnumerable<string>)f.Keywords)
            .Where(k => p.Length == 0 || k.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Select(g => new KeywordCount(g.Key, g.Count()))
            .OrderByDescending(kc => kc.Count);

        return Task.FromResult(toSeq(counts));
    }

    public Task<bool> UpdateAsync(Fact fact)
    {
        if (!_store.ContainsKey(fact.Id.Value))
            return Task.FromResult(false);
        _store[fact.Id.Value] = fact;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(FactId id) => Task.FromResult(_store.Remove(id.Value));
}
