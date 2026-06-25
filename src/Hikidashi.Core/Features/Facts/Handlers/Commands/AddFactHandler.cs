using Hikidashi.Core;
using LanguageExt;

namespace Hikidashi.Core.Facts;

public static class AddFactHandler<TRt>
    where TRt : IHasFactRepository, IHasClock, IHasIdGenerator
{
    public static Eff<TRt, Fact> Handle(AddFactCommand command) =>
        from parsed in FactCommandValidation.Parse(command).ToEff<TRt, AddFactParsed>()
        from id in IdGen<TRt>.NewId()
        from now in Clock<TRt>.UtcNow()
        // A fact captured without keywords is un-enriched, to be backfilled later via MCP.
        from fact in CommitAdd(
            new Fact(
                new FactId(id),
                parsed.Content,
                parsed.Keywords,
                Enriched: !parsed.Keywords.IsEmpty,
                "{}",
                now,
                now
            )
        )
        select fact;

    private static Eff<TRt, Fact> CommitAdd(Fact fact) =>
        from _ in FactRepo<TRt>.Add(fact)
        select fact;
}
