using Hikidashi.Core;
using Hikidashi.Core.Facts;

namespace Hikidashi.Core.Tests;

public sealed record TestRuntime(
    IFactRepository FactRepository,
    IClock Clock,
    IIdGenerator IdGenerator
) : IHasFactRepository, IHasClock, IHasIdGenerator
{
    public static TestRuntime Create() =>
        new(
            new InMemoryFactRepository(),
            new FixedClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new SequentialIdGenerator()
        );

    /// <summary>A runtime with a caller-supplied clock — used to observe timestamp behaviour.</summary>
    public static TestRuntime Create(IClock clock) =>
        new(new InMemoryFactRepository(), clock, new SequentialIdGenerator());
}

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
}

/// <summary>
/// A clock that hands out a distinct, monotonically increasing instant on every read so that
/// each create/update gets its own timestamp — lets tests pin recency and CreatedAt/UpdatedAt
/// behaviour that a <see cref="FixedClock"/> collapses.
/// </summary>
public sealed class AdvancingClock(DateTimeOffset start, TimeSpan? step = null) : IClock
{
    private readonly TimeSpan _step = step ?? TimeSpan.FromSeconds(1);
    private DateTimeOffset _now = start;

    public DateTimeOffset UtcNow
    {
        get
        {
            var current = _now;
            _now += _step;
            return current;
        }
    }
}

public sealed class SequentialIdGenerator : IIdGenerator
{
    private int _n;

    public Guid NewId()
    {
        _n++;
        return new Guid($"00000000-0000-0000-0000-{_n:D12}");
    }
}
