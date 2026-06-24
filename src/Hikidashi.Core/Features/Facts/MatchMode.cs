namespace Hikidashi.Core.Facts;

/// <summary>How multiple search terms combine. <see cref="Any"/> (OR) maximizes recall and is the default.</summary>
public enum MatchMode
{
    Any,
    All,
}
