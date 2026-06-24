using LanguageExt;

namespace Hikidashi.Core.Facts;

/// <summary>
/// The single persistence port for facts. Adapters (Postgres/Dapper) implement it; the domain
/// only ever sees this interface. Forgiving search lives here — see the adapter for the SQL.
/// </summary>
public interface IFactRepository
{
    Task<FactId> AddAsync(Fact fact);
    Task<Option<Fact>> FindByIdAsync(FactId id);
    Task<Seq<Fact>> SearchAsync(Seq<string> terms, MatchMode match, int limit);
    Task<Seq<Fact>> ListAsync(int limit, int offset);

    /// <summary>Facts captured without keywords, awaiting enrichment (most recent first).</summary>
    Task<Seq<Fact>> ListUnenrichedAsync(int limit);

    Task<Seq<KeywordCount>> ListKeywordsAsync(Option<string> prefix);

    /// <summary>Replaces the row; returns false if no fact with that id exists.</summary>
    Task<bool> UpdateAsync(Fact fact);

    /// <summary>Returns false if no fact with that id exists.</summary>
    Task<bool> DeleteAsync(FactId id);
}

public interface IHasFactRepository
{
    IFactRepository FactRepository { get; }
}
