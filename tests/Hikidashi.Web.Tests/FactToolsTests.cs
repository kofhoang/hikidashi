using Hikidashi.Core.Facts;

namespace Hikidashi.Web.Tests;

/// <summary>
/// Tool-layer tests — exercises FactTools directly (no MCP framework, no HTTP) to verify the
/// guards and response shapes that are invisible at the handler level.
/// </summary>
public class FactToolsTests
{
    private static AppRuntime Runtime() =>
        new(
            new InMemoryFactRepository(),
            new FixedClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
            new SequentialIdGenerator()
        );

    // ── Issue 2: empty keywords guard ─────────────────────────────────────

    [Fact]
    public async Task SearchFacts_empty_keywords_throws()
    {
        var rt = Runtime();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FactTools.SearchFacts(rt, [], "any", 20)
        );
        Assert.Contains("at least one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Issue 3: invalid match guard ──────────────────────────────────────

    [Fact]
    public async Task SearchFacts_invalid_match_throws()
    {
        var rt = Runtime();
        await FactTools.AddFact(rt, "content", ["kw"]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FactTools.SearchFacts(rt, ["kw"], "OR", 20)
        );
        Assert.Contains("match must be", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchFacts_match_is_case_insensitive_for_valid_values()
    {
        var rt = Runtime();
        await FactTools.AddFact(rt, "content", ["kw"]);

        var hits = await FactTools.SearchFacts(rt, ["kw"], "ANY", 20);
        Assert.Single(hits);

        hits = await FactTools.SearchFacts(rt, ["kw"], "ALL", 20);
        Assert.Single(hits);
    }

    // ── Issue 5: write tools return FactHit ───────────────────────────────

    [Fact]
    public async Task AddFact_returns_fact_hit_with_content_and_keywords()
    {
        var rt = Runtime();

        var hit = await FactTools.AddFact(rt, "wifi password is hunter2", ["wifi", "password"]);

        Assert.Equal("wifi password is hunter2", hit.Content);
        Assert.Equal(["wifi", "password"], hit.Keywords);
        Assert.False(string.IsNullOrEmpty(hit.Id));
    }

    [Fact]
    public async Task UpdateFact_returns_fact_hit_reflecting_changes()
    {
        var rt = Runtime();
        var added = await FactTools.AddFact(rt, "old content", ["old"]);

        var updated = await FactTools.UpdateFact(rt, added.Id, "new content", ["new"]);

        Assert.Equal(added.Id, updated.Id);
        Assert.Equal("new content", updated.Content);
        Assert.Equal(["new"], updated.Keywords);
    }

    [Fact]
    public async Task UpdateFact_null_fields_leave_existing_values_unchanged()
    {
        var rt = Runtime();
        var added = await FactTools.AddFact(rt, "original", ["kw1", "kw2"]);

        var updated = await FactTools.UpdateFact(rt, added.Id); // no content or keywords

        Assert.Equal("original", updated.Content);
        Assert.Equal(["kw1", "kw2"], updated.Keywords);
    }

    // ── Issue 1: empty keywords on update_fact un-enriches ────────────────

    [Fact]
    public async Task UpdateFact_empty_keywords_array_clears_keywords_and_unenriches()
    {
        var rt = Runtime();
        var added = await FactTools.AddFact(rt, "content", ["kw1", "kw2"]);

        await FactTools.UpdateFact(rt, added.Id, keywords: []);

        var detail = await FactTools.GetFact(rt, added.Id);
        Assert.Empty(detail.Keywords);
        Assert.False(detail.Enriched);
    }

    // ── Issue 6: timestamps in responses ─────────────────────────────────

    [Fact]
    public async Task AddFact_returns_non_empty_updated_at()
    {
        var rt = Runtime();

        var hit = await FactTools.AddFact(rt, "content", ["kw"]);

        Assert.False(string.IsNullOrEmpty(hit.UpdatedAt));
        Assert.True(DateTimeOffset.TryParse(hit.UpdatedAt, out _), "UpdatedAt should parse as ISO 8601");
    }

    [Fact]
    public async Task ListFacts_includes_updated_at()
    {
        var rt = Runtime();
        await FactTools.AddFact(rt, "first", ["a"]);
        await FactTools.AddFact(rt, "second", ["b"]);

        var hits = await FactTools.ListFacts(rt);

        Assert.All(hits, h => Assert.False(string.IsNullOrEmpty(h.UpdatedAt)));
    }

    [Fact]
    public async Task SearchFacts_includes_updated_at()
    {
        var rt = Runtime();
        await FactTools.AddFact(rt, "content about cats", ["cats"]);

        var hits = await FactTools.SearchFacts(rt, ["cats"]);

        Assert.Single(hits);
        Assert.False(string.IsNullOrEmpty(hits[0].UpdatedAt));
    }

    [Fact]
    public async Task GetFact_includes_updated_at()
    {
        var rt = Runtime();
        var added = await FactTools.AddFact(rt, "content", ["kw"]);

        var detail = await FactTools.GetFact(rt, added.Id);

        Assert.False(string.IsNullOrEmpty(detail.UpdatedAt));
        Assert.True(DateTimeOffset.TryParse(detail.UpdatedAt, out _), "UpdatedAt should parse as ISO 8601");
    }

    // ── Issue 4: FactDetail exposes enriched ──────────────────────────────

    [Fact]
    public async Task GetFact_enriched_true_when_keywords_supplied()
    {
        var rt = Runtime();
        var added = await FactTools.AddFact(rt, "content", ["kw"]);

        var detail = await FactTools.GetFact(rt, added.Id);

        Assert.True(detail.Enriched);
    }

    [Fact]
    public async Task GetFact_enriched_false_for_quick_capture()
    {
        var rt = Runtime();
        var added = await FactTools.AddFact(rt, "quick capture"); // no keywords

        var detail = await FactTools.GetFact(rt, added.Id);

        Assert.False(detail.Enriched);
    }

    // ── Malformed id guard: the model can pass anything as an id string ────

    [Fact]
    public async Task GetFact_rejects_a_malformed_id()
    {
        var rt = Runtime();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FactTools.GetFact(rt, "not-a-uuid")
        );
        Assert.Contains("not a valid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateFact_rejects_a_malformed_id()
    {
        var rt = Runtime();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FactTools.UpdateFact(rt, "not-a-uuid", "content")
        );
    }

    [Fact]
    public async Task DeleteFact_rejects_a_malformed_id()
    {
        var rt = Runtime();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FactTools.DeleteFact(rt, "not-a-uuid")
        );
    }

    // ── delete_fact: the destructive tool ─────────────────────────────────

    [Fact]
    public async Task DeleteFact_removes_the_fact_and_returns_its_id()
    {
        var rt = Runtime();
        var added = await FactTools.AddFact(rt, "to delete", ["kw"]);

        var result = await FactTools.DeleteFact(rt, added.Id);

        Assert.Equal(added.Id, result.Id);
        // The fact is really gone — a follow-up read fails.
        await Assert.ThrowsAsync<InvalidOperationException>(() => FactTools.GetFact(rt, added.Id));
    }

    [Fact]
    public async Task DeleteFact_on_a_missing_fact_is_an_error()
    {
        var rt = Runtime();
        var neverAdded = Guid.NewGuid().ToString();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FactTools.DeleteFact(rt, neverAdded)
        );
    }
}
