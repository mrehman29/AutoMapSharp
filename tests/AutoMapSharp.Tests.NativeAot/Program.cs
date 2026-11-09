using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AutoMapSharp;
using AutoMapSharp.Generated;

namespace AutoMapSharp.Tests.NativeAot;

public enum SourceTier { Gold, Silver }
public enum TargetTier { Silver, Gold }

public class NativeAddress { public string City { get; set; } = ""; }
public class NativeLine { public string Sku { get; set; } = ""; public int Quantity { get; set; } }

public class NativeUser
{
    public int Id { get; set; } = 42;
    public string Name { get; set; } = "Grace Hopper";
    public string Secret { get; set; } = "SuperSecret";
    public int? Rating { get; set; }
    public SourceTier Tier { get; set; }
    public NativeAddress? Address { get; set; }
    public List<NativeLine> Lines { get; set; } = new();
}

public class NativeAddressDto { public string City { get; set; } = ""; }
public class NativeLineDto { public string Sku { get; set; } = ""; public int Quantity { get; set; } }

public class NativeUserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Secret { get; set; } = "";
    public int? Rating { get; set; }
    public TargetTier Tier { get; set; }
    public string? AddressCity { get; set; }
    public NativeAddressDto? Address { get; set; }
    public List<NativeLineDto> Lines { get; set; } = new();
}

public class NativeModule : MappingModule
{
    public NativeModule()
    {
        DefineMap<NativeAddress, NativeAddressDto>();
        DefineMap<NativeLine, NativeLineDto>();
        DefineMap<NativeUser, NativeUserDto>()
            .ForProperty(d => d.Secret, opt => opt.Skip());
    }
}

/// <summary>
/// Runs as a Native AOT binary in CI. Every map here is emitted by the source generator, and the test fails if any
/// of them falls back to the engine's expression trees, which Native AOT cannot compile.
/// </summary>
public static class Program
{
    public static int Main()
    {
        Console.WriteLine($"[Native AOT Test] Dynamic code supported: {RuntimeFeature.IsDynamicCodeSupported}");
        var engine = new MapEngine(new NativeModule());

        foreach (var (source, destination) in new[]
                 {
                     (typeof(NativeUser), typeof(NativeUserDto)),
                     (typeof(NativeAddress), typeof(NativeAddressDto)),
                     (typeof(NativeLine), typeof(NativeLineDto))
                 })
        {
            var info = engine.GetPlanInfo(source, destination);
            Console.WriteLine($"[Native AOT Test] {source.Name} -> {destination.Name}: {info}");
            if (info.Kind != MapPlanKind.Generated)
            {
                Console.Error.WriteLine($"[FAIL] {source.Name} -> {destination.Name} is not generated: {info.Reason}");
                return 1;
            }
        }

        var user = new NativeUser
        {
            Id = 101,
            Name = "Margaret Hamilton",
            Secret = "TopSecret",
            Rating = 5,
            Tier = SourceTier.Gold,
            Address = new NativeAddress { City = "Cambridge" },
            Lines = { new NativeLine { Sku = "APOLLO-11", Quantity = 1 }, new NativeLine { Sku = "LM-5", Quantity = 2 } }
        };

        var dto = engine.Map<NativeUser, NativeUserDto>(user);

        var failures = new List<string>();
        if (dto.Id != 101 || dto.Name != "Margaret Hamilton") failures.Add($"members: Id={dto.Id}, Name={dto.Name}");
        if (dto.Secret != "") failures.Add($"Skip: Secret={dto.Secret}");
        if (dto.Rating != 5) failures.Add($"nullable: Rating={dto.Rating}");
        if (dto.Tier != TargetTier.Gold) failures.Add($"enum by name: Tier={dto.Tier}");
        if (dto.AddressCity != "Cambridge") failures.Add($"flattening: AddressCity={dto.AddressCity}");
        if (dto.Address?.City != "Cambridge") failures.Add($"nested object: Address.City={dto.Address?.City}");
        if (dto.Lines.Count != 2 || dto.Lines[1].Sku != "LM-5" || dto.Lines[1].Quantity != 2) failures.Add($"nested list: Count={dto.Lines.Count}");

        var nullUser = engine.Map<NativeUser, NativeUserDto>(new NativeUser { Address = null, Rating = null });
        if (nullUser.AddressCity != null || nullUser.Address != null || nullUser.Rating != null) failures.Add("null handling");

        foreach (var failure in failures)
        {
            Console.Error.WriteLine("[FAIL] " + failure);
        }

        if (failures.Count > 0)
        {
            return 2;
        }

        Console.WriteLine("[Native AOT Test] All generated maps ran correctly.");
        return 0;
    }
}
