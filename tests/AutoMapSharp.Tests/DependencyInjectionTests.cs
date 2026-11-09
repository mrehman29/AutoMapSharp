using System;
using AutoMapSharp.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.Tests;

/// <summary>
/// Dependency injection registration.
/// </summary>
public class DependencyInjectionTests
{
    public class Source { public string Name { get; set; } = ""; public decimal Net { get; set; } }
    public class Target { public string Name { get; set; } = ""; public decimal Gross { get; set; } }

    public interface IVatRate { decimal Rate { get; } }
    public class FixedVat : IVatRate { public decimal Rate => 0.2m; }

    public class GrossResolver : IPropertyResolver<Source, Target, decimal>
    {
        private readonly IVatRate _vat;
        public GrossResolver(IVatRate vat) { _vat = vat; }
        public decimal Resolve(Source source, Target destination, ResolutionContext context) => source.Net * (1 + _vat.Rate);
    }

    public class DiModule : MappingModule
    {
        public DiModule()
        {
            DefineMap<Source, Target>().ForProperty(d => d.Gross, o => o.MapWith<GrossResolver>());
        }
    }

    [Fact]
    public void NonModuleType_MarksItsAssemblyForScanning_LikeAutoMapper()
    {
        var services = new ServiceCollection();

        services.AddAutoMapSharp(typeof(DependencyInjectionTests));

        Assert.Contains(services, d => d.ServiceType == typeof(MappingModule) && d.ImplementationType == typeof(DiModule));
        Assert.Contains(services, d => d.ServiceType == typeof(GrossResolver));
    }

    [Fact]
    public void OptionsOverload_RegistersModulesAndInstances()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IVatRate, FixedVat>();

        services.AddAutoMapSharp(o => o.AddModule<DiModule>());
        using var provider = services.BuildServiceProvider();

        var result = provider.GetRequiredService<IMapEngine>().Map<Source, Target>(new Source { Name = "a", Net = 100m });

        Assert.Equal("a", result.Name);
        Assert.Equal(120m, result.Gross);
    }

    [Fact]
    public void TransientEngines_ShareOneConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IVatRate, FixedVat>();
        services.AddAutoMapSharp(typeof(DiModule));
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IMapEngine>();
        var second = provider.GetRequiredService<IMapEngine>();

        Assert.NotSame(first, second);
        Assert.Same(first.FindDescriptor(typeof(Source), typeof(Target)), second.FindDescriptor(typeof(Source), typeof(Target)));
    }

    [Fact]
    public void NoAssemblies_ThrowsInsteadOfGuessingTheCaller()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddAutoMapSharp(Array.Empty<System.Reflection.Assembly>()));
    }

    [Fact]
    public void WithServiceProvider_WithoutContainer_SharesPlans()
    {
        var root = new MapEngine(new DiModule());
        var services = new ServiceCollection().AddSingleton<IVatRate, FixedVat>().BuildServiceProvider();

        var scoped = root.WithServiceProvider(services);

        Assert.Equal(120m, scoped.Map<Source, Target>(new Source { Net = 100m }).Gross);
        Assert.Same(root.FindDescriptor(typeof(Source), typeof(Target)), scoped.FindDescriptor(typeof(Source), typeof(Target)));
    }
}
