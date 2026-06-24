using LanguageExt;

namespace Hikidashi.Core.Facts;

public static class ListFactsHandler<TRt>
    where TRt : IHasFactRepository
{
    public static Eff<TRt, Seq<Fact>> Handle(ListFactsQuery query) =>
        FactRepo<TRt>.List(ClampLimit(query.Limit), Math.Max(0, query.Offset));

    private static int ClampLimit(int limit) => Math.Clamp(limit <= 0 ? 50 : limit, 1, 500);
}
