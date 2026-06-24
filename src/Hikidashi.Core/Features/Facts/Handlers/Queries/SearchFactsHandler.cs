using LanguageExt;
using static LanguageExt.Prelude;

namespace Hikidashi.Core.Facts;

public static class SearchFactsHandler<TRt>
    where TRt : IHasFactRepository
{
    public static Eff<TRt, Seq<Fact>> Handle(SearchFactsQuery query)
    {
        var terms = Keywords.Normalize(query.Keywords);
        return terms.IsEmpty
            ? SuccessEff<TRt, Seq<Fact>>(LanguageExt.Seq<Fact>.Empty)
            : FactRepo<TRt>.Search(terms, query.Match, ClampLimit(query.Limit));
    }

    private static int ClampLimit(int limit) => Math.Clamp(limit <= 0 ? 20 : limit, 1, 200);
}
