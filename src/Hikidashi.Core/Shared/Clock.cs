using LanguageExt;

namespace Hikidashi.Core;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IHasClock
{
    IClock Clock { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Reads the current time from the runtime as an effect.</summary>
public static class Clock<TRt>
    where TRt : IHasClock
{
    public static Eff<TRt, DateTimeOffset> UtcNow() =>
        Eff<TRt, DateTimeOffset>.Lift(rt => rt.Clock.UtcNow);
}
