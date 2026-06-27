using Hikidashi.Core.Facts;
using LanguageExt;
using Xunit;
using static LanguageExt.Prelude;

namespace Hikidashi.Data.Tests;

/// <summary>
/// Exercises the real Postgres adapter — the SQL and Dapper/Npgsql mapping that the in-memory
/// Core tests can't cover. Skipped automatically when Docker is unavailable.
/// </summary>
[Collection(PostgresCollection.Name)]
public class FactRepositoryTests(PostgresFixture fx)
{
    private FactRepository Repo()
    {
        Skip.IfNot(fx.Available, "Docker/Postgres not available");
        return new FactRepository(fx.NewContext());
    }

    private static Fact NewFact(string content, params string[] keywords) =>
        new(
            new FactId(Guid.CreateVersion7()),
            content,
            toSeq(keywords),
            keywords.Length > 0,
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow
        );

    [SkippableFact]
    public async Task Add_then_find_roundtrips_verbatim()
    {
        var repo = Repo();
        var checklist = "1. Strip beds\n2. Vacuum\n3. Empty bins";
        var fact = NewFact(checklist, "cleaning", "checklist");

        await repo.AddAsync(fact);
        var found = await repo.FindByIdAsync(fact.Id);

        Assert.True(found.IsSome);
        var got = found.Match(Some: f => f, None: () => throw new Xunit.Sdk.XunitException("none"));
        Assert.Equal(checklist, got.Content);
        Assert.Equal(["cleaning", "checklist"], got.Keywords);
        Assert.Equal("{}", got.Metadata.Replace(" ", ""));
    }

    [SkippableFact]
    public async Task Search_any_ranks_by_term_hits()
    {
        var repo = Repo();
        await repo.AddAsync(NewFact("Only apples here.", "apple"));
        await repo.AddAsync(NewFact("Apples and bananas together.", "apple", "banana"));

        var hits = await repo.SearchAsync(toSeq(["apple", "banana"]), MatchMode.Any, 20);

        Assert.Equal(2, hits.Count);
        Assert.Contains("bananas", hits[0].Content);
    }

    [SkippableFact]
    public async Task Search_all_requires_every_term()
    {
        var repo = Repo();
        await repo.AddAsync(NewFact("Birthday cards in the drawer.", "cards"));
        await repo.AddAsync(NewFact("Spare key under the desk.", "key"));

        var all = await repo.SearchAsync(toSeq(["drawer", "cards"]), MatchMode.All, 20);

        Assert.Single(all);
        Assert.Contains("Birthday cards", all[0].Content);
    }

    [SkippableFact]
    public async Task Update_changes_content_and_keeps_others()
    {
        var repo = Repo();
        var fact = NewFact("old", "kw1", "kw2");
        await repo.AddAsync(fact);

        var ok = await repo.UpdateAsync(
            fact with
            {
                Content = "new",
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        var found = await repo.FindByIdAsync(fact.Id);

        Assert.True(ok);
        var got = found.Match(Some: f => f, None: () => throw new Xunit.Sdk.XunitException("none"));
        Assert.Equal("new", got.Content);
        Assert.Equal(["kw1", "kw2"], got.Keywords);
    }

    [SkippableFact]
    public async Task Delete_returns_false_for_missing()
    {
        var repo = Repo();
        Assert.False(await repo.DeleteAsync(new FactId(Guid.CreateVersion7())));
    }

    [SkippableFact]
    public async Task List_keywords_counts_and_prefix_filters()
    {
        var repo = Repo();
        await repo.AddAsync(NewFact("a", "birthday", "cards"));
        await repo.AddAsync(NewFact("b", "birthday", "wifi"));

        var prefixed = await repo.ListKeywordsAsync(Some("bir"));

        Assert.Contains(prefixed, k => k.Keyword == "birthday" && k.Count >= 2);
        Assert.DoesNotContain(prefixed, k => k.Keyword == "wifi");
    }

    // The forgiving-search promises below are SQL/ILIKE behaviour the in-memory fake can only
    // approximate — they're worth proving against real Postgres. Distinctive tokens keep the
    // assertions stable in the fixture's shared database.

    [SkippableFact]
    public async Task Search_matches_a_partial_substring_term()
    {
        var repo = Repo();
        await repo.AddAsync(NewFact("Spare key for the cupboard.", "zqxcupboard"));

        // "zqxcup" is only a prefix of the stored keyword — forgiving ILIKE must still find it.
        var hits = await repo.SearchAsync(toSeq(["zqxcup"]), MatchMode.Any, 20);

        Assert.Contains(hits, f => f.Content.Contains("cupboard"));
    }

    [SkippableFact]
    public async Task Search_is_case_insensitive()
    {
        var repo = Repo();
        await repo.AddAsync(NewFact("The ZQELEPHANT lives upstairs.", "animal"));

        var hits = await repo.SearchAsync(toSeq(["zqelephant"]), MatchMode.Any, 20);

        Assert.Contains(hits, f => f.Content.Contains("ZQELEPHANT"));
    }

    [SkippableFact]
    public async Task Search_matches_on_content_not_only_keywords()
    {
        var repo = Repo();
        // The search term appears in the content but in none of the keywords.
        await repo.AddAsync(NewFact("The zqumbrella is by the door.", "storage", "hallway"));

        var hits = await repo.SearchAsync(toSeq(["zqumbrella"]), MatchMode.Any, 20);

        Assert.Contains(hits, f => f.Content.Contains("zqumbrella"));
    }

    [SkippableFact]
    public async Task List_unenriched_returns_only_quick_captures()
    {
        var repo = Repo();
        var quick = NewFact("zqcapture awaiting keywords"); // no keywords -> un-enriched
        var enriched = NewFact("zqcapture already enriched", "zqkw");
        await repo.AddAsync(quick);
        await repo.AddAsync(enriched);

        var pending = await repo.ListUnenrichedAsync(500);

        Assert.Contains(pending, f => f.Id == quick.Id);
        Assert.DoesNotContain(pending, f => f.Id == enriched.Id);
    }
}
