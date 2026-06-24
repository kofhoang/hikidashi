using LanguageExt;
using static LanguageExt.Prelude;

namespace Hikidashi.Core.Facts;

/// <summary>
/// Strongly-typed fact identifier (UUIDv7). The implicit conversion to <see cref="Guid"/> lets it
/// unwrap at the persistence edge; <see cref="Parse"/> guards the string edge (MCP / REST input).
/// </summary>
public readonly record struct FactId(Guid Value)
{
    public override string ToString() => Value.ToString();

    public static implicit operator Guid(FactId id) => id.Value;

    public static Option<FactId> Parse(string? value) =>
        Guid.TryParse(value, out var g) ? Some(new FactId(g)) : None;
}
