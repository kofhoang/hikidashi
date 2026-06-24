using System.Linq;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Hikidashi.Core.Facts;

/// <summary>Pure keyword/term hygiene shared by capture and search: trim, drop blanks, de-dupe.</summary>
public static class Keywords
{
    public static Seq<string> Normalize(Seq<string> raw) =>
        toSeq(
            ((IEnumerable<string>)raw)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .DistinctBy(k => k.ToLowerInvariant())
        );
}
