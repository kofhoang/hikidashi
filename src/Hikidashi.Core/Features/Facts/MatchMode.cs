namespace Hikidashi.Core.Facts;

/// <summary>How multiple search terms combine. <see cref="Any"/> (OR) maximizes recall and is the default.</summary>
public abstract record MatchMode
{
    private protected MatchMode() { }

    public sealed record Any : MatchMode;

    public sealed record All : MatchMode;
}
