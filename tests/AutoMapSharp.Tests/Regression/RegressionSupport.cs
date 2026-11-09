using Xunit;

namespace AutoMapSharp.Tests.Regression;

/// <summary>
/// Tests that measure time or count process-wide events must not run in parallel with other tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelCollection
{
    public const string Name = "NonParallel";
}
