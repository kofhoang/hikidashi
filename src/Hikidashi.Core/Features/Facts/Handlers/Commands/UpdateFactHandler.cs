using Hikidashi.Core;
using LanguageExt;

namespace Hikidashi.Core.Facts;

public static class UpdateFactHandler<TRt>
    where TRt : IHasFactRepository, IHasClock
{
    public static Eff<TRt, Fact> Handle(UpdateFactCommand command) =>
        from parsed in FactCommandValidation.Parse(command).ToEff<TRt, UpdateFactParsed>()
        from existing in FactRepo<TRt>.FindOrError(command.Id)
        from now in Clock<TRt>.UtcNow()
        from fact in CommitUpdate(Apply(existing, parsed, now))
        select fact;

    private static Eff<TRt, Fact> CommitUpdate(Fact fact) =>
        from _ in FactRepo<TRt>.Update(fact)
        select fact;

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
