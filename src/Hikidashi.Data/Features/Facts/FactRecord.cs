namespace Hikidashi.Data;

/// <summary>
/// EF persistence entity for the <c>facts</c> table. A plain POCO kept separate from the domain
/// <see cref="Hikidashi.Core.Facts.Fact"/> so no ORM concerns leak into Core; the repository maps
/// between the two.
/// </summary>
public sealed class FactRecord
{
    public Guid Id { get; set; }
    public string Content { get; set; } = "";
    public string[] Keywords { get; set; } = [];
    public bool Enriched { get; set; }
    public string Metadata { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Keyless projection for the <c>list_keywords</c> aggregate (queried via raw SQL).</summary>
public sealed class KeywordCountRecord
{
    public string Keyword { get; set; } = "";
    public int Count { get; set; }
}
