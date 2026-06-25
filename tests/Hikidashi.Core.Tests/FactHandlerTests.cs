using Hikidashi.Core;
using Hikidashi.Core.Facts;
using LanguageExt;
using LanguageExt.Common;
using Xunit;
using static LanguageExt.Prelude;

namespace Hikidashi.Core.Tests;

public class FactHandlerTests
{
    private static async Task<T> Succ<T>(Eff<TestRuntime, T> eff, TestRuntime rt) =>
        (await eff.RunAsync(rt)).Match(
            Succ: x => x,
            Fail: e => throw new Xunit.Sdk.XunitException($"expected success, got: {e}")
        );

    private static async Task<Error> Fail<T>(Eff<TestRuntime, T> eff, TestRuntime rt) =>
        (await eff.RunAsync(rt)).Match(
            Succ: _ => throw new Xunit.Sdk.XunitException("expected failure, got success"),
            Fail: e => e
        );

    private static async Task<FactId> Add(
        TestRuntime rt,
        string content,
        params string[] keywords
    ) =>
        (
            await Succ(
                AddFactHandler<TestRuntime>.Handle(new AddFactCommand(content, toSeq(keywords))),
                rt
            )
        ).Id;

    [Fact]
    public async Task Add_then_get_returns_content_verbatim()
    {
        var rt = TestRuntime.Create();
        var checklist = "1. Strip beds\n2. Vacuum\n3. Empty bins";

        var id = await Add(rt, checklist, "cleaning", "checklist");
        var fact = await Succ(GetFactHandler<TestRuntime>.Handle(new GetFactQuery(id)), rt);

        Assert.Equal(checklist, fact.Content);
        Assert.Equal(["cleaning", "checklist"], fact.Keywords);
    }

    [Fact]
    public async Task Add_with_blank_content_fails_validation()
    {
        var rt = TestRuntime.Create();

        var err = await Fail(
            AddFactHandler<TestRuntime>.Handle(new AddFactCommand("   ", toSeq(["x"]))),
            rt
        );

        Assert.IsType<ValidationError>(err);
    }

    [Fact]
    public async Task Add_normalizes_keywords()
    {
        var rt = TestRuntime.Create();

        var id = await Add(rt, "answer", " Birthday ", "birthday", "", "cards");
        var fact = await Succ(GetFactHandler<TestRuntime>.Handle(new GetFactQuery(id)), rt);

        Assert.Equal(["Birthday", "cards"], fact.Keywords);
    }

    [Fact]
    public async Task Search_any_matches_on_keyword_or_content()
    {
        var rt = TestRuntime.Create();
        await Add(rt, "We keep birthday cards in the hallway drawer.", "cards", "storage");
        await Add(rt, "The wifi password is hunter2.", "wifi", "password");

        var hits = await Succ(
            SearchFactsHandler<TestRuntime>.Handle(
                new SearchFactsQuery(toSeq(["drawer", "nonsense"]), MatchMode.Any, 20)
            ),
            rt
        );

        Assert.Single(hits);
        Assert.Contains("birthday cards", hits[0].Content);
    }

    [Fact]
    public async Task Search_all_requires_every_term()
    {
        var rt = TestRuntime.Create();
        await Add(rt, "Birthday cards live in the drawer.", "cards");
        await Add(rt, "Spare drawer key is taped under the desk.", "key", "drawer");

        var any = await Succ(
            SearchFactsHandler<TestRuntime>.Handle(
                new SearchFactsQuery(toSeq(["drawer", "cards"]), MatchMode.Any, 20)
            ),
            rt
        );
        var all = await Succ(
            SearchFactsHandler<TestRuntime>.Handle(
                new SearchFactsQuery(toSeq(["drawer", "cards"]), MatchMode.All, 20)
            ),
            rt
        );

        Assert.Equal(2, any.Count);
        Assert.Single(all);
        Assert.Contains("Birthday cards", all[0].Content);
    }

    [Fact]
    public async Task Search_ranks_by_term_hit_count()
    {
        var rt = TestRuntime.Create();
        await Add(rt, "Only mentions apples.", "apple");
        await Add(rt, "Mentions apples and bananas together.", "apple", "banana");

        var hits = await Succ(
            SearchFactsHandler<TestRuntime>.Handle(
                new SearchFactsQuery(toSeq(["apple", "banana"]), MatchMode.Any, 20)
            ),
            rt
        );

        Assert.Equal(2, hits.Count);
        Assert.Contains("bananas", hits[0].Content); // two hits ranks first
    }

    [Fact]
    public async Task Search_with_no_usable_terms_returns_empty_without_touching_repo()
    {
        var rt = TestRuntime.Create();
        await Add(rt, "something", "x");

        var hits = await Succ(
            SearchFactsHandler<TestRuntime>.Handle(
                new SearchFactsQuery(toSeq(["  ", ""]), MatchMode.Any, 20)
            ),
            rt
        );

        Assert.True(hits.IsEmpty);
    }

    [Fact]
    public async Task Update_applies_partial_overrides_and_keeps_the_rest()
    {
        var rt = TestRuntime.Create();
        var id = await Add(rt, "old answer", "kw1", "kw2");

        await Succ(
            UpdateFactHandler<TestRuntime>.Handle(
                new UpdateFactCommand(id, Some("new answer"), Option<Seq<string>>.None)
            ),
            rt
        );
        var fact = await Succ(GetFactHandler<TestRuntime>.Handle(new GetFactQuery(id)), rt);

        Assert.Equal("new answer", fact.Content);
        Assert.Equal(["kw1", "kw2"], fact.Keywords); // keywords untouched
    }

    [Fact]
    public async Task Update_missing_fact_returns_not_found()
    {
        var rt = TestRuntime.Create();
        var missing = new FactId(Guid.NewGuid());

        var err = await Fail(
            UpdateFactHandler<TestRuntime>.Handle(
                new UpdateFactCommand(missing, Some("x"), Option<Seq<string>>.None)
            ),
            rt
        );

        Assert.IsType<NotFoundError>(err);
    }

    [Fact]
    public async Task Delete_removes_the_fact_then_missing_is_not_found()
    {
        var rt = TestRuntime.Create();
        var id = await Add(rt, "to delete", "x");

        await Succ(DeleteFactHandler<TestRuntime>.Handle(new DeleteFactCommand(id)), rt);

        var err = await Fail(GetFactHandler<TestRuntime>.Handle(new GetFactQuery(id)), rt);
        Assert.IsType<NotFoundError>(err);
    }

    [Fact]
    public async Task Delete_missing_fact_returns_not_found()
    {
        var rt = TestRuntime.Create();

        var err = await Fail(
            DeleteFactHandler<TestRuntime>.Handle(
                new DeleteFactCommand(new FactId(Guid.NewGuid()))
            ),
            rt
        );

        Assert.IsType<NotFoundError>(err);
    }

    [Fact]
    public async Task List_keywords_counts_and_filters_by_prefix()
    {
        var rt = TestRuntime.Create();
        await Add(rt, "a", "birthday", "cards");
        await Add(rt, "b", "birthday", "wifi");

        var all = await Succ(
            ListKeywordsHandler<TestRuntime>.Handle(new ListKeywordsQuery(Option<string>.None)),
            rt
        );
        var birthday = all.Find(k => k.Keyword == "birthday");

        Assert.True(birthday.IsSome);
        Assert.Equal(2, birthday.Match(Some: k => k.Count, None: () => 0));

        var prefixed = await Succ(
            ListKeywordsHandler<TestRuntime>.Handle(new ListKeywordsQuery(Some("bir"))),
            rt
        );
        Assert.Single(prefixed);
        Assert.Equal("birthday", prefixed[0].Keyword);
    }

    [Fact]
    public async Task Quick_capture_without_keywords_is_unenriched_and_listed()
    {
        var rt = TestRuntime.Create();
        var quick = await Add(rt, "wifi password is hunter2"); // no keywords
        await Add(rt, "enriched one", "kw"); // has keywords

        var detail = await Succ(GetFactHandler<TestRuntime>.Handle(new GetFactQuery(quick)), rt);
        Assert.False(detail.Enriched);

        var pending = await Succ(
            ListUnenrichedFactsHandler<TestRuntime>.Handle(new ListUnenrichedFactsQuery(50)),
            rt
        );
        Assert.Single(pending);
        Assert.Equal(quick, pending[0].Id);
    }

    [Fact]
    public async Task AddHandler_returns_full_fact_with_content_and_keywords()
    {
        var rt = TestRuntime.Create();

        var fact = await Succ(
            AddFactHandler<TestRuntime>.Handle(
                new AddFactCommand("my content", toSeq(["kw1", "kw2"]))
            ),
            rt
        );

        Assert.Equal("my content", fact.Content);
        Assert.Equal(["kw1", "kw2"], fact.Keywords);
        Assert.True(fact.Enriched);
    }

    [Fact]
    public async Task UpdateHandler_returns_updated_fact()
    {
        var rt = TestRuntime.Create();
        var id = await Add(rt, "old content", "kw1");

        var updated = await Succ(
            UpdateFactHandler<TestRuntime>.Handle(
                new UpdateFactCommand(id, Some("new content"), Some(toSeq(["kw2", "kw3"])))
            ),
            rt
        );

        Assert.Equal("new content", updated.Content);
        Assert.Equal(["kw2", "kw3"], updated.Keywords);
        Assert.True(updated.Enriched);
    }

    [Fact]
    public async Task Update_with_empty_keywords_clears_keywords_and_unenriches()
    {
        var rt = TestRuntime.Create();
        var id = await Add(rt, "answer", "kw1", "kw2");

        await Succ(
            UpdateFactHandler<TestRuntime>.Handle(
                new UpdateFactCommand(id, Option<string>.None, Some(toSeq(new string[0])))
            ),
            rt
        );
        var fact = await Succ(GetFactHandler<TestRuntime>.Handle(new GetFactQuery(id)), rt);

        Assert.Empty(fact.Keywords);
        Assert.False(fact.Enriched);
    }

    [Fact]
    public async Task Enriching_via_update_clears_it_from_the_unenriched_list()
    {
        var rt = TestRuntime.Create();
        var id = await Add(rt, "wifi password is hunter2"); // un-enriched

        await Succ(
            UpdateFactHandler<TestRuntime>.Handle(
                new UpdateFactCommand(id, Option<string>.None, Some(toSeq(["wifi", "password"])))
            ),
            rt
        );

        var detail = await Succ(GetFactHandler<TestRuntime>.Handle(new GetFactQuery(id)), rt);
        Assert.True(detail.Enriched);

        var pending = await Succ(
            ListUnenrichedFactsHandler<TestRuntime>.Handle(new ListUnenrichedFactsQuery(50)),
            rt
        );
        Assert.True(pending.IsEmpty);
    }
}
