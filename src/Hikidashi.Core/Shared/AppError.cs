using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Hikidashi.Core;

/// <summary>
/// Domain errors as a sealed DU over <see cref="Expected"/>. The <c>private protected</c>
/// constructor means only this assembly can introduce new cases. Handlers fail with these;
/// the MCP boundary translates them into structured tool errors.
/// </summary>
public abstract record AppError : Expected
{
    private protected AppError(string message, int statusCode)
        : base(message, statusCode, None) { }
}

public sealed record NotFoundError(string Entity, string Id)
    : AppError($"{Entity} '{Id}' not found", 404);

public sealed record ValidationError : AppError
{
    public Seq<string> Messages { get; }

    public ValidationError(string message)
        : base(message, 400) => Messages = [message];

    public ValidationError(Seq<string> messages)
        : base(string.Join("; ", messages), 400) => Messages = messages;
}
