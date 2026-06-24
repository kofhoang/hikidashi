using LanguageExt;

namespace Hikidashi.Core.Facts;

public static class ListUnenrichedFactsHandler<TRt>
    where TRt : IHasFactRepository
{
    public static Eff<TRt, Seq<Fact>> Handle(ListUnenrichedFactsQuery query) =>
        FactRepo<TRt>.ListUnenriched(ClampLimit(query.Limit));

    private static int ClampLimit(int limit) => Math.Clamp(limit <= 0 ? 50 : limit, 1, 500);
}
