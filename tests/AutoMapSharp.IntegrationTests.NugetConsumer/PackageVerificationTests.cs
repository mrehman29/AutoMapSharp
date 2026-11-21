using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapSharp;
using AutoMapSharp.Collections;
using AutoMapSharp.Extensions.DependencyInjection;
using AutoMapSharp.Generated;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.IntegrationTests.NugetConsumer;

public class PackageVerificationTests
{
    public class OrderItem
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
    }

    public class Order
    {
        public int OrderId { get; set; }
        public List<OrderItem> Items { get; set; } = new();
    }

    public class OrderDto
    {
        public int OrderId { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class VerificationMappingModule : MappingModule
    {
        public VerificationMappingModule()
        {
            // Note: Define explicitly without .ReverseMap() to avoid registering OrderItemDto -> OrderItem twice
            DefineMap<OrderItem, OrderItemDto>();
            DefineMap<OrderItemDto, OrderItem>().EqualityComparison((dto, item) => dto.Id == item.Id);
            DefineMap<OrderDto, Order>();
        }
    }

    [Fact]
    public void PackageMetadata_HasInformationalVersionAndCommitSha()
    {
        var coreAsm = typeof(MapEngine).Assembly;
        var collectionsAsm = typeof(CollectionMappingExtensions).Assembly;
        var diAsm = typeof(AutoMapSharpServiceCollectionExtensions).Assembly;
        var expectedSha = Environment.GetEnvironmentVariable("EXPECTED_COMMIT_SHA");

        foreach (var asm in new[] { coreAsm, collectionsAsm, diAsm })
        {
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            
            Assert.NotNull(infoVer);
            Assert.NotEmpty(infoVer);
            
            // .NET SDK 8+ always includes +<commit-sha> when building inside a git repository
            Assert.Contains("+", infoVer);
            var commitSha = infoVer[(infoVer.IndexOf('+') + 1)..];
            Assert.True(commitSha.Length >= 7, $"Commit SHA '{commitSha}' should be at least 7 characters in {asm.GetName().Name}.");

            // If EXPECTED_COMMIT_SHA is passed by verify-packages.sh, the full HEAD SHA must start with
            // the (possibly abbreviated) SHA embedded in the assembly.
            if (!string.IsNullOrEmpty(expectedSha))
            {
                Assert.True(
                    expectedSha.StartsWith(commitSha, StringComparison.OrdinalIgnoreCase),
                    $"Assembly {asm.GetName().Name} was built from commit '{commitSha}', expected HEAD commit '{expectedSha}'.");
            }
        }
    }

    [Fact]
    public void PackageAssembly_HasExpectedAssemblyAndVersionMetadata()
    {
        var coreAsm = typeof(MapEngine).Assembly;
        var collectionsAsm = typeof(CollectionMappingExtensions).Assembly;
        var diAsm = typeof(AutoMapSharpServiceCollectionExtensions).Assembly;

        // Ensure assemblies are distinct loaded assemblies
        Assert.NotSame(coreAsm, collectionsAsm);
        Assert.NotSame(coreAsm, diAsm);
        Assert.NotSame(collectionsAsm, diAsm);

        // Verify assembly location exists and is populated
        Assert.False(string.IsNullOrWhiteSpace(coreAsm.Location));
        Assert.False(string.IsNullOrWhiteSpace(collectionsAsm.Location));
        Assert.False(string.IsNullOrWhiteSpace(diAsm.Location));

        // When running against consumed packages, verify expected version prefix across all three assemblies
        var expectedVersion = Environment.GetEnvironmentVariable("EXPECTED_PACKAGE_VERSION");

        foreach (var asm in new[] { coreAsm, collectionsAsm, diAsm })
        {
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            
            if (!string.IsNullOrEmpty(expectedVersion))
            {
                // Strict check: package version followed by "+" separator
                Assert.StartsWith(expectedVersion + "+", infoVer, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Version-agnostic: semver (with optional prerelease) followed by "+" build metadata
                Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?\+", infoVer);
            }
        }

        if (string.IsNullOrEmpty(expectedVersion))
        {
            // All three packages are built together and must report the same InformationalVersion
            var coreInfoVer = coreAsm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Assert.Equal(coreInfoVer, collectionsAsm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            Assert.Equal(coreInfoVer, diAsm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        }
    }

    [Fact]
    public void PackageAssembly_SourceGenerator_EmitsGeneratedMapsUnderDefaultExecution()
    {
        var isGeneratedDisabled = string.Equals(
            Environment.GetEnvironmentVariable("AUTOMAPSHARP_DISABLE_GENERATED"),
            "1",
            StringComparison.Ordinal);

        var engine = new MapEngine(new VerificationMappingModule());
        var plan = engine.GetPlanInfo(typeof(OrderItem), typeof(OrderItemDto));

        if (isGeneratedDisabled)
        {
            Assert.Equal(MapPlanKind.Compiled, plan.Kind);
        }
        else
        {
            // When generator is active, MapPlanKind is Generated
            Assert.Equal(MapPlanKind.Generated, plan.Kind);
        }

        var item = new OrderItem { Id = 101, Sku = "WIDGET-01", UnitPrice = 19.99m };
        var dto = engine.Map<OrderItem, OrderItemDto>(item);
        Assert.NotNull(dto);
        Assert.Equal(101, dto.Id);
        Assert.Equal("WIDGET-01", dto.Sku);
        Assert.Equal(19.99m, dto.UnitPrice);
    }

    [Fact]
    public void PackageAssembly_CollectionsMergingAndDependencyInjectionIntegration()
    {
        // 1. Verify DI container registration
        var services = new ServiceCollection();
        services.AddAutoMapSharp(typeof(VerificationMappingModule));
        using var sp = services.BuildServiceProvider();

        var engine = sp.GetRequiredService<IMapEngine>();
        Assert.NotNull(engine);

        // 2. Exercise AutoMapSharp.Collections EqualityComparison merge semantics
        var keptItem = new OrderItem { Id = 10, Sku = "KEPT-OLD", UnitPrice = 5.0m };
        var removedItem = new OrderItem { Id = 20, Sku = "REMOVED", UnitPrice = 10.0m };
        var existingOrder = new Order
        {
            OrderId = 1,
            Items = new List<OrderItem> { keptItem, removedItem }
        };

        var incomingOrderDto = new OrderDto
        {
            OrderId = 1,
            Items = new List<OrderItemDto>
            {
                new OrderItemDto { Id = 10, Sku = "KEPT-UPDATED", UnitPrice = 7.5m }, // Update existing
                new OrderItemDto { Id = 30, Sku = "ADDED-NEW", UnitPrice = 15.0m }    // Add new
            }
        };

        engine.Map(incomingOrderDto, existingOrder);

        // Assert keptItem was updated in place (same reference)
        Assert.Equal(2, existingOrder.Items.Count);
        Assert.Same(keptItem, existingOrder.Items[0]);
        Assert.Equal("KEPT-UPDATED", keptItem.Sku);
        Assert.Equal(7.5m, keptItem.UnitPrice);

        // Assert removedItem was removed and new item was added
        Assert.Equal(new[] { 10, 30 }, existingOrder.Items.Select(i => i.Id).ToArray());
        Assert.Equal("ADDED-NEW", existingOrder.Items[1].Sku);
    }
}
