using LanguageExt;

namespace Hikidashi.Core.Facts;

public static class ListKeywordsHandler<TRt>
    where TRt : IHasFactRepository
{
    public static Eff<TRt, Seq<KeywordCount>> Handle(ListKeywordsQuery query) =>
        FactRepo<TRt>.ListKeywords(query.Prefix.Map(p => p.Trim()).Filter(p => p.Length > 0));
}
