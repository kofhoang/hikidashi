using Hikidashi.Core;
using LanguageExt;

namespace Hikidashi.Core.Facts;

/// <summary>Lifts the <see cref="IFactRepository"/> port into runtime effects for handlers to compose.</summary>
public static class FactRepo<TRt>
    where TRt : IHasFactRepository
{
    public static Eff<TRt, FactId> Add(Fact fact) =>
        Eff<TRt, FactId>.LiftIO(rt => rt.FactRepository.AddAsync(fact));

    public static Eff<TRt, Fact> FindOrError(FactId id) =>
        Eff<TRt, Option<Fact>>
            .LiftIO(rt => rt.FactRepository.FindByIdAsync(id))
            .OrError(new NotFoundError("Fact", id.ToString()));

    public static Eff<TRt, Seq<Fact>> Search(Seq<string> terms, MatchMode match, int limit) =>
        Eff<TRt, Seq<Fact>>.LiftIO(rt => rt.FactRepository.SearchAsync(terms, match, limit));

    public static Eff<TRt, Seq<Fact>> List(int limit, int offset) =>
        Eff<TRt, Seq<Fact>>.LiftIO(rt => rt.FactRepository.ListAsync(limit, offset));

    public static Eff<TRt, Seq<Fact>> ListUnenriched(int limit) =>
        Eff<TRt, Seq<Fact>>.LiftIO(rt => rt.FactRepository.ListUnenrichedAsync(limit));

    public static Eff<TRt, Seq<KeywordCount>> ListKeywords(Option<string> prefix) =>
        Eff<TRt, Seq<KeywordCount>>.LiftIO(rt => rt.FactRepository.ListKeywordsAsync(prefix));

    public static Eff<TRt, bool> Update(Fact fact) =>
        Eff<TRt, bool>.LiftIO(rt => rt.FactRepository.UpdateAsync(fact));

    public static Eff<TRt, bool> Delete(FactId id) =>
        Eff<TRt, bool>.LiftIO(rt => rt.FactRepository.DeleteAsync(id));
}
