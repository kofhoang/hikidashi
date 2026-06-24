using Hikidashi.Core;
using LanguageExt;

namespace Hikidashi.Core.Facts;

public static class AddFactHandler<TRt>
    where TRt : IHasFactRepository, IHasClock, IHasIdGenerator
{
    public static Eff<TRt, FactId> Handle(AddFactCommand command) =>
        from parsed in FactCommandValidation.Parse(command).ToEff<TRt, AddFactParsed>()
        from id in IdGen<TRt>.NewId()
        from now in Clock<TRt>.UtcNow()
        // A fact captured without keywords is un-enriched, to be backfilled later via MCP.
        from saved in FactRepo<TRt>.Add(
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
        select saved;
}
