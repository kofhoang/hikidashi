using System.ComponentModel;
using System.Linq;
using Hikidashi.Core;
using Hikidashi.Core.Facts;
using LanguageExt;
using ModelContextProtocol.Server;
using static LanguageExt.Prelude;

namespace Hikidashi.Web;

/// <summary>
/// The MCP tool surface — the heart of hikidashi. The DESCRIPTIONS are the interface: they tell the
/// calling model how to behave, not just what each tool is. Read tools are marked read-only; delete
/// is marked destructive. Content is always returned verbatim.
/// </summary>
[McpServerToolType]
public static class FactTools
{
    [McpServerTool(Name = "search_facts", ReadOnly = true)]
    [Description(
        "Search stored facts by keyword; returns matching items with id, content, keywords, and "
            + "last-updated timestamp. Matching is forgiving: substring search over both the stored "
            + "keywords and the content text, so partial terms and synonyms work. Pass several "
            + "keywords including synonyms and different question phrasings to maximize recall. "
            + "Content is returned verbatim and complete — relay it as-is, never summarize unless "
            + "asked. Requires at least one keyword; an empty array is an error. "
            + "FALLBACK: if results are empty or weak, call list_keywords to discover the available "
            + "vocabulary, then search again using those terms."
    )]
    public static async Task<FactHit[]> SearchFacts(
        AppRuntime rt,
        [Description(
            "One or more search terms — synonyms and question phrasings improve recall. "
                + "An empty array is an error."
        )]
            string[] keywords,
        [Description(
            "\"any\" (default, OR — maximizes recall) or \"all\" (AND — every term must match). "
                + "Any other value is an error."
        )]
            string match = "any",
        [Description("Maximum number of facts to return (default 20).")] int limit = 20
    )
    {
        var facts = await Run(
            rt,
            from kws in ParseKeywords(keywords)
            from mode in ParseMatch(match)
            from results in SearchFactsHandler<AppRuntime>.Handle(
                new SearchFactsQuery(kws, mode, limit)
            )
            select results
        );
        return facts.Map(ToHit).ToArray();
    }

    [McpServerTool(Name = "list_keywords", ReadOnly = true)]
    [Description(
        "List every keyword in the store with its count, optionally filtered by prefix. Use this "
            + "to discover the user's vocabulary when a search returns weak or empty results, then "
            + "search again using those terms."
    )]
    public static async Task<KeywordCount[]> ListKeywords(
        AppRuntime rt,
        [Description("Optional case-insensitive prefix filter.")] string? prefix = null
    )
    {
        var counts = await Run(
            rt,
            ListKeywordsHandler<AppRuntime>.Handle(new ListKeywordsQuery(Optional(prefix)))
        );
        return counts.ToArray();
    }

    [McpServerTool(Name = "get_fact", ReadOnly = true)]
    [Description(
        "Retrieve one fact in full by its id. Returns verbatim content, keywords, enriched status "
            + "(false = quick capture without keywords; search coverage is limited until keywords "
            + "are added), metadata (reserved for future use; always {}), and last-updated "
            + "timestamp. Use the id returned by search_facts, list_facts, add_fact, or update_fact."
    )]
    public static async Task<FactDetail> GetFact(
        AppRuntime rt,
        [Description(
            "The fact id (uuid), as returned by search_facts, list_facts, add_fact, or update_fact."
        )]
            string id
    )
    {
        var fact = await Run(
            rt,
            from fid in ParseIdEff(id)
            from f in GetFactHandler<AppRuntime>.Handle(new GetFactQuery(fid))
            select f
        );
        return new FactDetail(
            fact.Id.ToString(),
            fact.Content,
            fact.Keywords.ToArray(),
            fact.Enriched,
            fact.Metadata,
            fact.UpdatedAt.ToString("O")
        );
    }

    [McpServerTool(Name = "list_facts", ReadOnly = true)]
    [Description(
        "List stored facts most-recently-updated first, with id, content, keywords, and timestamp. "
            + "For browsing; prefer search_facts to answer specific questions."
    )]
    public static async Task<FactHit[]> ListFacts(
        AppRuntime rt,
        [Description("Maximum number to return (default 50).")] int limit = 50,
        [Description("Number to skip, for paging (default 0).")] int offset = 0
    )
    {
        var facts = await Run(
            rt,
            ListFactsHandler<AppRuntime>.Handle(new ListFactsQuery(limit, offset))
        );
        return facts.Map(ToHit).ToArray();
    }

    [McpServerTool(Name = "add_fact")]
    [Description(
        "Store a new durable fact. Content is saved verbatim — never summarize or truncate it "
            + "(store whole checklists in full). Normally supply GENEROUS keywords: synonyms plus "
            + "the different questions the user might later ask to find this. Keywords are trimmed "
            + "and case-insensitive duplicates are dropped. Returns the stored fact (id, content, "
            + "keywords, timestamp) for immediate confirmation. You MAY omit keywords for a quick "
            + "capture — the fact is flagged un-enriched and can be backfilled later via "
            + "list_unenriched_facts."
    )]
    public static async Task<FactHit> AddFact(
        AppRuntime rt,
        [Description("The answer to store, verbatim.")] string content,
        [Description(
            "Generous findability terms: synonyms and question phrasings. Omit (null) for a quick "
                + "capture; an empty array has the same effect as omitting."
        )]
            string[]? keywords = null
    )
    {
        var fact = await Run(
            rt,
            AddFactHandler<AppRuntime>.Handle(new AddFactCommand(content, toSeq(keywords ?? [])))
        );
        return ToHit(fact);
    }

    [McpServerTool(Name = "list_unenriched_facts", ReadOnly = true)]
    [Description(
        "List facts that were captured WITHOUT keywords and are awaiting enrichment, most recent "
            + "first. For each, read the content and call update_fact with generous keywords "
            + "(synonyms + question phrasings) — that marks the fact enriched. Use this to clean "
            + "up quick captures so they become findable."
    )]
    public static async Task<FactHit[]> ListUnenrichedFacts(
        AppRuntime rt,
        [Description("Maximum number to return (default 50).")] int limit = 50
    )
    {
        var facts = await Run(
            rt,
            ListUnenrichedFactsHandler<AppRuntime>.Handle(new ListUnenrichedFactsQuery(limit))
        );
        return facts.Map(ToHit).ToArray();
    }

    [McpServerTool(Name = "update_fact", Idempotent = true)]
    [Description(
        "Update a fact's content and/or keywords. Omit a field entirely (null) to leave it "
            + "unchanged; returns the fact after update. IMPORTANT: passing an empty keywords "
            + "array ([]) CLEARS all keywords and marks the fact un-enriched — omit keywords "
            + "(null) to keep the existing ones. When changing content, keep it verbatim and "
            + "re-supply generous keywords if the topic shifted."
    )]
    public static async Task<FactHit> UpdateFact(
        AppRuntime rt,
        [Description(
            "The fact id (uuid), as returned by search_facts, list_facts, add_fact, or a prior update_fact."
        )]
            string id,
        [Description("New verbatim content, or omit (null) to keep the existing content.")]
            string? content = null,
        [Description(
            "Replacement keywords, or omit entirely (null) to keep existing ones. "
                + "Passing an empty array ([]) CLEARS all keywords and marks the fact un-enriched."
        )]
            string[]? keywords = null
    )
    {
        var fact = await Run(
            rt,
            from fid in ParseIdEff(id)
            from f in UpdateFactHandler<AppRuntime>.Handle(
                new UpdateFactCommand(
                    fid,
                    Optional(content),
                    keywords is null ? Option<Seq<string>>.None : Some(toSeq(keywords))
                )
            )
            select f
        );
        return ToHit(fact);
    }

    [McpServerTool(Name = "delete_fact", Destructive = true)]
    [Description(
        "Permanently delete a fact by id. This cannot be undone — confirm intent before calling."
    )]
    public static async Task<IdResult> DeleteFact(
        AppRuntime rt,
        [Description("The fact id (uuid).")] string id
    )
    {
        var deletedId = await Run(
            rt,
            from fid in ParseIdEff(id)
            from _ in DeleteFactHandler<AppRuntime>.Handle(new DeleteFactCommand(fid))
            select fid
        );
        return new IdResult(deletedId.ToString());
    }

    private static Eff<AppRuntime, Seq<string>> ParseKeywords(string[] keywords)
    {
        var kws = toSeq(keywords);
        return kws.IsEmpty
            ? FailEff<AppRuntime, Seq<string>>(
                new ValidationError(
                    "keywords must contain at least one term; an empty array always returns nothing."
                )
            )
            : SuccessEff<AppRuntime, Seq<string>>(kws);
    }

    private static Eff<AppRuntime, MatchMode> ParseMatch(string match) =>
        match.ToLowerInvariant() switch
        {
            "any" => SuccessEff<AppRuntime, MatchMode>(new MatchMode.Any()),
            "all" => SuccessEff<AppRuntime, MatchMode>(new MatchMode.All()),
            _ => FailEff<AppRuntime, MatchMode>(
                new ValidationError($"match must be \"any\" or \"all\"; got \"{match}\".")
            ),
        };

    private static Eff<AppRuntime, FactId> ParseIdEff(string id) =>
        FactId
            .Parse(id)
            .Match(
                Some: fid => SuccessEff<AppRuntime, FactId>(fid),
                None: () =>
                    FailEff<AppRuntime, FactId>(
                        new ValidationError($"'{id}' is not a valid fact id.")
                    )
            );

    private static FactHit ToHit(Fact f) =>
        new(f.Id.ToString(), f.Content, f.Keywords.ToArray(), f.UpdatedAt.ToString("O"));

    private static async Task<T> Run<T>(AppRuntime rt, Eff<AppRuntime, T> eff) =>
        (await eff.RunAsync(rt)).Match(
            Succ: x => x,
            Fail: e => throw new InvalidOperationException(e.Message)
        );
}

public record FactHit(string Id, string Content, string[] Keywords, string UpdatedAt);

public record FactDetail(
    string Id,
    string Content,
    string[] Keywords,
    bool Enriched,
    string Metadata,
    string UpdatedAt
);

public record IdResult(string Id);
