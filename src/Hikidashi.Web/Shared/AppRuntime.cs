using Hikidashi.Core;
using Hikidashi.Core.Facts;

namespace Hikidashi.Web;

/// <summary>
/// The composition-root runtime handed to every effect. hikidashi is a pure MCP server — no
/// server-side LLM — so the runtime is just storage + clock + id generation.
/// </summary>
public sealed record AppRuntime(
    IFactRepository FactRepository,
    IClock Clock,
    IIdGenerator IdGenerator
) : IHasFactRepository, IHasClock, IHasIdGenerator;
