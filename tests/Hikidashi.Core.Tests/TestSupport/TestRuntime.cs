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
}

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
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
