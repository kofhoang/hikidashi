using Hikidashi.Core;
using LanguageExt;

namespace Hikidashi.Core.Facts;

public static class UpdateFactHandler<TRt>
    where TRt : IHasFactRepository, IHasClock
{
    public static Eff<TRt, FactId> Handle(UpdateFactCommand command) =>
        from parsed in FactCommandValidation.Parse(command).ToEff<TRt, UpdateFactParsed>()
        from existing in FactRepo<TRt>.FindOrError(command.Id)
        from now in Clock<TRt>.UtcNow()
        from _ in FactRepo<TRt>.Update(Apply(existing, parsed, now))
        select existing.Id;

    private static Fact Apply(Fact existing, UpdateFactParsed parsed, DateTimeOffset now)
    {
        var keywords = parsed.Keywords.IfNone(existing.Keywords);
        return existing with
        {
            Content = parsed.Content.Match(Some: c => (string)c, None: () => existing.Content),
            Keywords = keywords,
            // Supplying keywords enriches the fact; clearing them marks it un-enriched again.
            Enriched = !keywords.IsEmpty,
            UpdatedAt = now,
        };
    }
}
