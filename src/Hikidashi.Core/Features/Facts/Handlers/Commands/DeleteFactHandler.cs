using LanguageExt;
using static LanguageExt.Prelude;

namespace Hikidashi.Core.Facts;

public static class DeleteFactHandler<TRt>
    where TRt : IHasFactRepository
{
    public static Eff<TRt, Unit> Handle(DeleteFactCommand command) =>
        from existing in FactRepo<TRt>.FindOrError(command.Id)
        from _ in FactRepo<TRt>.Delete(command.Id)
        select unit;
}
