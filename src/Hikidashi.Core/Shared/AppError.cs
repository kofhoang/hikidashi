using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Hikidashi.Core;

/// <summary>
/// Domain errors as <see cref="Expected"/> subtypes carrying the HTTP status they map to.
/// Handlers fail with these; the web boundary translates them to responses and the MCP
/// boundary turns them into structured tool errors.
/// </summary>
public record NotFoundError(string Entity, string Id)
    : Expected($"{Entity} '{Id}' not found", 404, None);

public record ValidationError : Expected
{
    public Seq<string> Messages { get; }

    public ValidationError(string message)
        : base(message, 400, None) => Messages = [message];

    public ValidationError(Seq<string> messages)
        : base(string.Join("; ", messages), 400, None) => Messages = messages;
}
