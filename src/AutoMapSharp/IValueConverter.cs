namespace AutoMapSharp;

/// <summary>
/// Converts one source member value to one destination member value. Unlike <see cref="IPropertyResolver{TSource, TDestination, TDestMember}"/>,
/// it only sees the member value, so one converter can be reused across many maps. Equivalent to AutoMapper's <c>IValueConverter</c>.
/// </summary>
public interface IValueConverter<in TSourceMember, out TDestinationMember>
{
    /// <summary>
    /// Converts the source member value.
    /// </summary>
    TDestinationMember Convert(TSourceMember sourceMember, ResolutionContext context);
}
