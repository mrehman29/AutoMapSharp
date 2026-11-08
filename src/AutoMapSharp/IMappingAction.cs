namespace AutoMapSharp;

/// <summary>
/// A reusable BeforeMap or AfterMap step, registered with <c>BeforeMap&lt;TAction&gt;()</c> or <c>AfterMap&lt;TAction&gt;()</c>.
/// Instances are created from the service provider when registered there, so they can take constructor dependencies.
/// Equivalent to AutoMapper's <c>IMappingAction</c>.
/// </summary>
public interface IMappingAction<in TSource, in TDestination>
{
    /// <summary>
    /// Runs the step.
    /// </summary>
    void Process(TSource source, TDestination destination, ResolutionContext context);
}
