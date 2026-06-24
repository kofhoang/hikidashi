using LanguageExt;

namespace Hikidashi.Core.Facts;

/// <summary>
/// A durable reference fact. <see cref="Content"/> is the answer, stored and returned verbatim
/// (never summarized). <see cref="Keywords"/> are generous findability terms. <see cref="Enriched"/>
/// is false for a quick capture stored without keywords — the model later backfills keywords via MCP
/// and flips it true. <see cref="Metadata"/> is raw jsonb (kept as a string so no JSON library leaks
/// into the domain) — an evolvability hedge.
/// </summary>
public record Fact(
    FactId Id,
    string Content,
    Seq<string> Keywords,
    bool Enriched,
    string Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>A keyword and how many facts carry it — the shape returned by list_keywords.</summary>
public record KeywordCount(string Keyword, int Count);
