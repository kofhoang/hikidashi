using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Hikidashi.Core;

/// <summary>
/// LINQ/binding helpers that let <see cref="Either{L, R}"/>, <see cref="Option{A}"/>, and the
/// applicative <see cref="Validation{F, A}"/> flow directly into an <see cref="Eff{RT, A}"/>
/// query expression without explicit wrapping. Lifted from the house functional style.
/// </summary>
public static class EitherEffExtensions
{
    // Handles Either<Error, T> — the base case.
    public static Eff<TRt, TFinal> SelectMany<TRt, T, TResult, TFinal>(
        this Eff<TRt, T> eff,
        Func<T, Either<Error, TResult>> bind,
        Func<T, TResult, TFinal> project
    ) => eff.SelectMany(t => Eff<TResult>.Lift(() => bind(t)), project);

    // Handles Either<L, T> where L : Error — covers Option.ToEither(concreteError).
    public static Eff<TRt, TFinal> SelectMany<TRt, T, L, TResult, TFinal>(
        this Eff<TRt, T> eff,
        Func<T, Either<L, TResult>> bind,
        Func<T, TResult, TFinal> project
    )
        where L : Error =>
        eff.Bind(t =>
            bind(t)
                .Match(
                    Right: r => SuccessEff<TRt, TFinal>(project(t, r)),
                    Left: e => FailEff<TRt, TFinal>((Error)e)
                )
        );

    /// <summary>Fails with a <see cref="NotFoundError"/>-style error when the option is None.</summary>
    public static Eff<TRt, T> OrError<TRt, T>(this Eff<TRt, Option<T>> eff, Error error) =>
        eff.SelectMany(opt => opt.ToEither(error), (_, value) => value);

    /// <summary>
    /// Lifts an applicative <see cref="Validation{F, A}"/> into an Eff, collapsing all accumulated
    /// failures into a single <see cref="ValidationError"/> (400) so every broken rule surfaces together.
    /// </summary>
    public static Eff<TRt, T> ToEff<TRt, T>(this Validation<Seq<Error>, T> validation) =>
        validation.Match(
            Succ: SuccessEff<TRt, T>,
            Fail: errors => FailEff<TRt, T>(new ValidationError(errors.Map(e => e.Message)))
        );
}
