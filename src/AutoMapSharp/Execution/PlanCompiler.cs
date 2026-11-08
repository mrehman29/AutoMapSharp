using System;
using System.Linq.Expressions;

namespace AutoMapSharp.Execution;

/// <summary>
/// Compiles mapping plans. Set the environment variable <c>AUTOMAPSHARP_PREFER_INTERPRETATION=1</c> to run plans
/// through the expression interpreter instead of compiled IL. That is the path platforms without dynamic code,
/// such as Native AOT, use automatically, and CI runs the full test suite in both modes.
/// </summary>
internal static class PlanCompiler
{
    /// <summary>
    /// True when plans run through the expression interpreter rather than compiled IL.
    /// </summary>
    public static bool Interpreted =>
#if NETSTANDARD2_0
        PreferInterpretation;
#else
        PreferInterpretation || !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled;
#endif

    public static readonly bool PreferInterpretation =
        string.Equals(Environment.GetEnvironmentVariable("AUTOMAPSHARP_PREFER_INTERPRETATION"), "1", StringComparison.Ordinal);

    public static TDelegate Compile<TDelegate>(Expression<TDelegate> lambda) where TDelegate : Delegate
    {
#if NETSTANDARD2_0
        return lambda.Compile();
#else
        return lambda.Compile(PreferInterpretation);
#endif
    }

    public static Delegate Compile(LambdaExpression lambda)
    {
#if NETSTANDARD2_0
        return lambda.Compile();
#else
        return lambda.Compile(PreferInterpretation);
#endif
    }
}
