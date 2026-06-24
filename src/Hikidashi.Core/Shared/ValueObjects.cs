using LanguageExt;
using static LanguageExt.Prelude;

namespace Hikidashi.Core;

/// <summary>
/// A string guaranteed non-blank and trimmed. Construct via <see cref="From"/>; the implicit
/// conversion lets it unwrap to <see cref="string"/> at the edges without scattering .Value.
/// </summary>
public readonly record struct NonEmptyString
{
    public string Value { get; }

    private NonEmptyString(string value) => Value = value;

    public static Option<NonEmptyString> From(string? value) =>
        string.IsNullOrWhiteSpace(value) ? None : Some(new NonEmptyString(value.Trim()));

    public override string ToString() => Value;

    public static implicit operator string(NonEmptyString s) => s.Value;
}
