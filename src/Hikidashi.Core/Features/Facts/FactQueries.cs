using LanguageExt;

namespace Hikidashi.Core.Facts;

public record SearchFactsQuery(Seq<string> Keywords, MatchMode Match, int Limit);

public record GetFactQuery(FactId Id);

public record ListFactsQuery(int Limit, int Offset);

public record ListUnenrichedFactsQuery(int Limit);

public record ListKeywordsQuery(Option<string> Prefix);
