using LanguageExt;

namespace Hikidashi.Core.Facts;

public static class GetFactHandler<TRt>
    where TRt : IHasFactRepository
{
    public static Eff<TRt, Fact> Handle(GetFactQuery query) => FactRepo<TRt>.FindOrError(query.Id);
}
