using Hikidashi.Core.Facts;
using LanguageExt;
using LanguageExt.Common;
using Xunit;
using static LanguageExt.Prelude;

namespace Hikidashi.Core.Tests;

/// <summary>
/// Pins the parse layer ("parse, don't validate"): refined commands are correct by construction,
/// so every caller — MCP, capture, tests — inherits the same invariants. These are pure and fast.
/// </summary>
public class FactCommandValidationTests
{
    private static T Succ<T>(Validation<Seq<Error>, T> v) =>
        v.Match(
            Succ: x => x,
            Fail: e => throw new Xunit.Sdk.XunitException($"expected success, got: {e}")
        );

    private static bool IsFail<T>(Validation<Seq<Error>, T> v) =>
        v.Match(Succ: _ => false, Fail: _ => true);

    [Fact]
    public void Add_trims_content_and_normalizes_keywords()
    {
        var parsed = Succ(
            FactCommandValidation.Parse(
                new AddFactCommand("  the answer  ", toSeq([" Birthday ", "birthday", "", "cards"]))
            )
        );

        Assert.Equal("the answer", (string)parsed.Content); // surrounding whitespace trimmed
        Assert.Equal(["Birthday", "cards"], parsed.Keywords); // trimmed, de-duped (case-insensitive), blanks dropped
    }

    [Fact]
    public void Add_with_blank_content_fails()
    {
        Assert.True(IsFail(FactCommandValidation.Parse(new AddFactCommand("   ", toSeq(["x"])))));
    }

    [Fact]
    public void Update_omitting_content_keeps_it_unset()
    {
        var parsed = Succ(
            FactCommandValidation.Parse(
                new UpdateFactCommand(
                    new FactId(Guid.NewGuid()),
                    Option<string>.None,
                    Option<Seq<string>>.None
                )
            )
        );

        // None means "leave unchanged" — it must not collapse into a blank-content failure.
        Assert.True(parsed.Content.IsNone);
        Assert.True(parsed.Keywords.IsNone);
    }

    [Fact]
    public void Update_with_supplied_blank_content_fails()
    {
        var result = FactCommandValidation.Parse(
            new UpdateFactCommand(
                new FactId(Guid.NewGuid()),
                Some("   "),
                Option<Seq<string>>.None
            )
        );

        // A supplied-but-blank content is a real error, distinct from omitting the field.
        Assert.True(IsFail(result));
    }

    [Fact]
    public void Update_normalizes_supplied_keywords()
    {
        var parsed = Succ(
            FactCommandValidation.Parse(
                new UpdateFactCommand(
                    new FactId(Guid.NewGuid()),
                    Some("  fresh content  "),
                    Some(toSeq([" wifi ", "wifi", "  ", "password"]))
                )
            )
        );

        Assert.Equal("fresh content", parsed.Content.Match(Some: c => (string)c, None: () => ""));
        Assert.Equal(
            ["wifi", "password"],
            parsed.Keywords.Match(Some: k => k, None: () => Seq<string>.Empty)
        );
    }
}
