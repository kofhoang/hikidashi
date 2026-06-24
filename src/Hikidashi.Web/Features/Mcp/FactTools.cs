using System.ComponentModel;
using System.Linq;
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
        "Search the user's stored personal facts by keyword. Matching is forgiving — substring over "
            + "both the keywords and the answer text — so partial terms work. Pass several keywords, "
            + "including synonyms and natural phrasings of the question. IMPORTANT: if the results are "
            + "empty or weak, call list_keywords to see which topics exist, then search again using that "
            + "vocabulary. The returned `keywords` reflect the user's own wording — reuse them. `content` "
            + "is the stored answer, returned verbatim and complete (e.g. whole checklists); relay it as-is, "
            + "never summarize it unless asked."
    )]
    public static async Task<FactHit[]> SearchFacts(
        AppRuntime rt,
        [Description("Search terms: synonyms and question phrasings improve recall.")]
            string[] keywords,
        [Description(
            "\"any\" (default, OR — maximizes recall) or \"all\" (AND — every term must match)."
        )]
            string match = "any",
        [Description("Maximum number of facts to return (default 20).")] int limit = 20
    )
    {
        var mode = string.Equals(match, "all", StringComparison.OrdinalIgnoreCase)
            ? MatchMode.All
            : MatchMode.Any;
        var facts = await Run(
            rt,
            SearchFactsHandler<AppRuntime>.Handle(
                new SearchFactsQuery(toSeq(keywords), mode, limit)
            )
        );
        return facts.Map(ToHit).ToArray();
    }

    [McpServerTool(Name = "list_keywords", ReadOnly = true)]
    [Description(
        "List every keyword in the store with its count, optionally filtered by prefix. Use this to "
            + "discover the user's vocabulary when a search returns weak or empty results, then search again."
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
        "Get one fact in full by its id. Returns the verbatim content, keywords, and metadata."
    )]
    public static async Task<FactDetail> GetFact(
        AppRuntime rt,
        [Description("The fact id (uuid).")] string id
    )
    {
        var fid = ParseId(id);
        var fact = await Run(rt, GetFactHandler<AppRuntime>.Handle(new GetFactQuery(fid)));
        return new FactDetail(
            fact.Id.ToString(),
            fact.Content,
            fact.Keywords.ToArray(),
            fact.Enriched,
            fact.Metadata
        );
    }

    [McpServerTool(Name = "list_facts", ReadOnly = true)]
    [Description(
        "List stored facts, most recently updated first. For browsing; prefer search_facts to answer questions."
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
        "Store a new durable fact. `content` is the answer, saved and returned verbatim — never summarize "
            + "or truncate it (store whole checklists in full). Normally supply GENEROUS keywords: synonyms "
            + "plus the different questions the user might later ask to find this. You MAY omit keywords for a "
            + "quick capture — the fact is then marked un-enriched and can be backfilled later via "
            + "list_unenriched_facts. Returns the new fact id."
    )]
    public static async Task<IdResult> AddFact(
        AppRuntime rt,
        [Description("The answer to store, verbatim.")] string content,
        [Description(
            "Generous findability terms: synonyms and question phrasings. Omit for a quick capture."
        )]
            string[]? keywords = null
    )
    {
        var id = await Run(
            rt,
            AddFactHandler<AppRuntime>.Handle(new AddFactCommand(content, toSeq(keywords ?? [])))
        );
        return new IdResult(id.ToString());
    }

    [McpServerTool(Name = "list_unenriched_facts", ReadOnly = true)]
    [Description(
        "List facts that were captured WITHOUT keywords and are awaiting enrichment, most recent first. "
            + "For each, read the content and call update_fact with generous keywords (synonyms + question "
            + "phrasings) — that marks the fact enriched. Use this to clean up quick captures so they become findable."
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
        "Update a fact's content and/or keywords by id. Omit a field to leave it unchanged. When you "
            + "change content, keep it verbatim and re-supply generous keywords if the topic shifted."
    )]
    public static async Task<IdResult> UpdateFact(
        AppRuntime rt,
        [Description("The fact id (uuid).")] string id,
        [Description("New verbatim content, or omit to keep the existing content.")]
            string? content = null,
        [Description("Replacement keywords, or omit to keep the existing keywords.")]
            string[]? keywords = null
    )
    {
        var fid = ParseId(id);
        var updated = await Run(
            rt,
            UpdateFactHandler<AppRuntime>.Handle(
                new UpdateFactCommand(
                    fid,
                    Optional(content),
                    keywords is null ? Option<Seq<string>>.None : Some(toSeq(keywords))
                )
            )
        );
        return new IdResult(updated.ToString());
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
        var fid = ParseId(id);
        await Run(rt, DeleteFactHandler<AppRuntime>.Handle(new DeleteFactCommand(fid)));
        return new IdResult(fid.ToString());
    }

    private static FactHit ToHit(Fact f) => new(f.Id.ToString(), f.Content, f.Keywords.ToArray());

    private static FactId ParseId(string id) =>
        FactId
            .Parse(id)
            .Match(
                Some: f => f,
                None: () => throw new InvalidOperationException($"'{id}' is not a valid fact id.")
            );

    private static async Task<T> Run<T>(AppRuntime rt, Eff<AppRuntime, T> eff) =>
        (await eff.RunAsync(rt)).Match(
            Succ: x => x,
            Fail: e => throw new InvalidOperationException(e.Message)
        );
}

public record FactHit(string Id, string Content, string[] Keywords);

public record FactDetail(
    string Id,
    string Content,
    string[] Keywords,
    bool Enriched,
    string Metadata
);

public record IdResult(string Id);
