using AutoMapSharp.Extensions.DependencyInjection;
using AutoMapSharp.Sample.Web.Models;

namespace AutoMapSharp.Sample.Web.Infrastructure;

/// <summary>
/// The sample app's mapping-related service registrations. Program.cs and the composition-root test both call this,
/// so the test always checks the registrations the app really uses.
/// </summary>
public static class SampleServiceRegistration
{
    public static IServiceCollection AddSampleMappingServices(this IServiceCollection services)
    {
        // Scan this assembly for modules, resolvers, converters and mapping actions.
        // IMapEngine is transient and resolves dependencies from the current request's scope.
        services.AddAutoMapSharp(typeof(SampleServiceRegistration).Assembly);

        // A scoped service used by RequestIdResolver on the Reliability > Scoped Services page.
        services.AddScoped<RequestTrace>();

        // Used by the StampExportTime mapping action on the Parity > Mapping Actions page.
        services.AddSingleton<ISampleClock, FixedSampleClock>();

        // Used by WebTaxResolver on the Resolvers page.
        services.AddSingleton<ITaxRateService, MockTaxRateService>();

        return services;
    }
}
