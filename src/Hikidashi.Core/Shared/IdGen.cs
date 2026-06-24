using LanguageExt;

namespace Hikidashi.Core;

public interface IIdGenerator
{
    Guid NewId();
}

public interface IHasIdGenerator
{
    IIdGenerator IdGenerator { get; }
}

/// <summary>Time-ordered UUIDv7 ids — index-friendly and sortable by creation order.</summary>
public sealed class SystemIdGenerator : IIdGenerator
{
    public static readonly SystemIdGenerator Instance = new();

    public Guid NewId() => Guid.CreateVersion7();
}

/// <summary>Generates a new id from the runtime as an effect.</summary>
public static class IdGen<TRt>
    where TRt : IHasIdGenerator
{
    public static Eff<TRt, Guid> NewId() => Eff<TRt, Guid>.Lift(rt => rt.IdGenerator.NewId());
}
