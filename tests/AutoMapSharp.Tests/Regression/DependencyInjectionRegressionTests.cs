using System;
using System.Linq;
using AutoMapSharp.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class DependencyInjectionRegressionTests
{
    public class RequestInfo { public Guid RequestId { get; } = Guid.NewGuid(); }
    public interface ITaxService { decimal Rate { get; } }
    public class FixedTaxService : ITaxService { public decimal Rate => 0.25m; }

    public class Invoice { public decimal Net { get; set; } }
    public class InvoiceDto { public Guid RequestId { get; set; } public decimal Tax { get; set; } }

    public class RequestIdResolver : IPropertyResolver<Invoice, InvoiceDto, Guid>
    {
        private readonly RequestInfo _request;
        public RequestIdResolver(RequestInfo request) { _request = request; }
        public Guid Resolve(Invoice source, InvoiceDto destination, ResolutionContext context) => _request.RequestId;
    }

    public class TaxResolver : IPropertyResolver<Invoice, InvoiceDto, decimal>
    {
        private readonly ITaxService _tax;
        public TaxResolver(ITaxService tax) { _tax = tax; }
        public decimal Resolve(Invoice source, InvoiceDto destination, ResolutionContext context) => source.Net * _tax.Rate;
    }

    public class ScopedModule : MappingModule
    {
        public ScopedModule()
        {
            DefineMap<Invoice, InvoiceDto>()
                .ForProperty(d => d.RequestId, o => o.MapWith<RequestIdResolver>())
                .ForProperty(d => d.Tax, o => o.Skip());
        }
    }

    public class UnregisteredResolverModule : MappingModule
    {
        public UnregisteredResolverModule()
        {
            DefineMap<Invoice, InvoiceDto>()
                .ForProperty(d => d.RequestId, o => o.Skip())
                .ForProperty(d => d.Tax, o => o.MapWith<TaxResolver>());
        }
    }

    [Fact]
    [Trait("Finding", "F19")]
    public void F19_ResolverWithScopedDependency_ResolvesFromCallerScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<RequestInfo>();
        services.AddTransient<RequestIdResolver>();
        services.AddAutoMapSharp(typeof(ScopedModule));
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        Guid MapIn(IServiceScope scope, out Guid scopedId)
        {
            scopedId = scope.ServiceProvider.GetRequiredService<RequestInfo>().RequestId;
            var engine = scope.ServiceProvider.GetRequiredService<IMapEngine>();
            return engine.Map<Invoice, InvoiceDto>(new Invoice()).RequestId;
        }

        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var idA = MapIn(scopeA, out var expectedA);
        var idB = MapIn(scopeB, out var expectedB);

        Assert.Equal(expectedA, idA);
        Assert.Equal(expectedB, idB);
        Assert.NotEqual(idA, idB);
    }

    [Fact]
    [Trait("Finding", "F20")]
    public void F20_UnregisteredResolverWithDependencies_IsCreatedFromContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaxService, FixedTaxService>();
        services.AddAutoMapSharp(typeof(UnregisteredResolverModule));
        using var provider = services.BuildServiceProvider();

        var engine = provider.GetRequiredService<IMapEngine>();
        var result = engine.Map<Invoice, InvoiceDto>(new Invoice { Net = 100m });

        Assert.Equal(25m, result.Tax);
    }

    [Fact]
    [Trait("Finding", "F20")]
    public void F20_AssemblyScan_FindsInternalModules()
    {
        var services = new ServiceCollection();

        services.AddAutoMapSharp(typeof(DependencyInjectionRegressionTests).Assembly);

        Assert.Contains(services, d => d.ServiceType == typeof(MappingModule) && d.ImplementationType == typeof(InternalScanModule));
    }
}

internal sealed class InternalScanModule : MappingModule
{
    public InternalScanModule() { DefineMap<DependencyInjectionRegressionTests.Invoice, DependencyInjectionRegressionTests.InvoiceDto>(); }
}
