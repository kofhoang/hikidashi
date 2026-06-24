using LanguageExt;

namespace Hikidashi.Core.Facts;

public record AddFactCommand(string Content, Seq<string> Keywords);

public record UpdateFactCommand(FactId Id, Option<string> Content, Option<Seq<string>> Keywords);

public record DeleteFactCommand(FactId Id);
