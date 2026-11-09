using Xunit;

namespace AutoMapSharp.Tests.Regression;

/// <summary>
/// Skip reasons for regression tests whose fix has not landed yet.
/// See KNOWN_ISSUES.md for the findings register.
/// Every Skip must be removed once its fix lands.
/// </summary>
internal static class Pending
{
    public const string Phase10 = "Open finding. Fixed in remediation Phase 10 (hotfixes).";
    public const string Phase11 = "Open finding. Fixed in remediation Phase 11 (execution plan engine).";
    public const string Phase12 = "Open finding. Fixed in remediation Phase 12 (DI correctness).";
    public const string Phase13 = "Open finding. Fixed in remediation Phase 13 (parity tier 1).";
    public const string Phase14 = "Open finding. Fixed in remediation Phase 14 (parity tier 2).";
}

/// <summary>
/// Tests that measure time or count process-wide events must not run in parallel with other tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelCollection
{
    public const string Name = "NonParallel";
}
