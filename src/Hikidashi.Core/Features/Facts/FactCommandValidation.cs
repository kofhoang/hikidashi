using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Hikidashi.Core.Facts;

/// <summary>
/// Parses raw commands into refined ones (parse, don't validate): content becomes a
/// <see cref="NonEmptyString"/> and keywords are normalized, so handlers receive values that are
/// correct by construction. Guards the invariants for every caller — MCP, the capture route, and tests.
/// </summary>
public record AddFactParsed(NonEmptyString Content, Seq<string> Keywords);

public record UpdateFactParsed(Option<NonEmptyString> Content, Option<Seq<string>> Keywords);

public static class FactCommandValidation
{
    public static Validation<Seq<Error>, AddFactParsed> Parse(AddFactCommand c) =>
        Content(c.Content)
            .Map(content => new AddFactParsed(content, Keywords.Normalize(c.Keywords)))
            .As();

    public static Validation<Seq<Error>, UpdateFactParsed> Parse(UpdateFactCommand c) =>
        ContentOpt(c.Content)
            .Map(content => new UpdateFactParsed(content, c.Keywords.Map(Keywords.Normalize)))
            .As();

    private static Validation<Seq<Error>, NonEmptyString> Content(string content) =>
        NonEmptyString
            .From(content)
            .Match(
                Some: Success<Seq<Error>, NonEmptyString>,
                None: () =>
                    Fail<Seq<Error>, NonEmptyString>([Error.New("Content must not be empty")])
            );

    private static Validation<Seq<Error>, Option<NonEmptyString>> ContentOpt(
        Option<string> content
    ) =>
        content.Match(
            Some: v =>
                NonEmptyString
                    .From(v)
                    .Match(
                        Some: s => Success<Seq<Error>, Option<NonEmptyString>>(Some(s)),
                        None: () =>
                            Fail<Seq<Error>, Option<NonEmptyString>>([
                                Error.New("Content must not be empty"),
                            ])
                    ),
            None: () => Success<Seq<Error>, Option<NonEmptyString>>(None)
        );
}
