using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapSharp;
using AutoMapSharp.Configuration;
using Xunit;

namespace AutoMapSharp.Tests;

public class Phase4Tests
{
    public class ItemSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class ItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class OrderSource
    {
        public int OrderId { get; set; }
        public List<ItemSource>? Items { get; set; }
        public HashSet<string>? Tags { get; set; }
        public Dictionary<string, ItemSource>? FeatureDict { get; set; }
    }

    public class OrderDto
    {
        public int OrderId { get; set; }
        public List<ItemDto>? Items { get; set; }
        public HashSet<string>? Tags { get; set; }
        public Dictionary<string, ItemDto>? FeatureDict { get; set; }
    }

    public class ImmutableTarget
    {
        public string Code { get; }
        public decimal ScaledValue { get; }

        public ImmutableTarget(string code, decimal scaledValue)
        {
            Code = code;
            ScaledValue = scaledValue;
        }
    }

    // Circular reference models
    public class NodeSource
    {
        public string Name { get; set; } = string.Empty;
        public NodeSource? Parent { get; set; }
        public List<NodeSource> Children { get; set; } = new();
    }

    public class NodeDto
    {
        public string Name { get; set; } = string.Empty;
        public NodeDto? Parent { get; set; }
        public List<NodeDto> Children { get; set; } = new();
    }

    // 4-level hierarchy
    public class OrgSource
    {
        public string OrgName { get; set; } = string.Empty;
        public DivSource? Division { get; set; }
    }
    public class DivSource
    {
        public string DivName { get; set; } = string.Empty;
        public DeptSource? Department { get; set; }
    }
    public class DeptSource
    {
        public string DeptName { get; set; } = string.Empty;
        public ManagerSource? Manager { get; set; }
    }
    public class ManagerSource
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class OrgDto
    {
        public string OrgName { get; set; } = string.Empty;
        public DivDto? Division { get; set; }
    }
    public class DivDto
    {
        public string DivName { get; set; } = string.Empty;
        public DeptDto? Department { get; set; }
    }
    public class DeptDto
    {
        public string DeptName { get; set; } = string.Empty;
        public ManagerDto? Manager { get; set; }
    }
    public class ManagerDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class Phase4Module : MappingModule
    {
        public Phase4Module()
        {
            DefineMap<ItemSource, ItemDto>();
            DefineMap<OrderSource, OrderDto>();

            // ConstructUsing
            DefineMap<ItemSource, ImmutableTarget>()
                .ConstructUsing(src => new ImmutableTarget(src.Name.ToUpper(), src.Price * 1.1m));

            // Deep hierarchy
            DefineMap<OrgSource, OrgDto>();
            DefineMap<DivSource, DivDto>();
            DefineMap<DeptSource, DeptDto>();
            DefineMap<ManagerSource, ManagerDto>();

            // Circular reference with PreserveReferences
            DefineMap<NodeSource, NodeDto>()
                .PreserveReferences(true)
                .MaxDepth(5);
        }
    }

    public class NullCollectionModule : MappingModule
    {
        public NullCollectionModule()
        {
            DefineMap<OrderSource, OrderDto>()
                .AllowNullCollections(true);
        }
    }

    [Fact]
    public void Task4_1_StandardCollectionMapping_ArraysAndLists()
    {
        var engine = new MapEngine(new[] { new Phase4Module() });
        var sources = new List<ItemSource>
        {
            new() { Id = 1, Name = "Laptop", Price = 1200m },
            new() { Id = 2, Name = "Mouse", Price = 25m }
        };

        // Top-level List mapping
        var dtos = engine.Map<List<ItemSource>, List<ItemDto>>(sources);
        Assert.NotNull(dtos);
        Assert.Equal(2, dtos.Count);
        Assert.Equal("Laptop", dtos[0].Name);
        Assert.Equal(25m, dtos[1].Price);

        // Top-level Array mapping
        var sourceArray = sources.ToArray();
        var dtoArray = engine.Map<ItemSource[], ItemDto[]>(sourceArray);
        Assert.NotNull(dtoArray);
        Assert.Equal(2, dtoArray.Length);
        Assert.Equal("Mouse", dtoArray[1].Name);
    }

    [Fact]
    public void Task4_2_ReadOnlyCollections_SetsAndDictionaries()
    {
        var engine = new MapEngine(new[] { new Phase4Module() });
        var order = new OrderSource
        {
            OrderId = 101,
            Tags = new HashSet<string> { "Electronics", "Sale", "Electronics" }, // Deduplicated
            FeatureDict = new Dictionary<string, ItemSource>
            {
                ["primary"] = new() { Id = 1, Name = "Monitor", Price = 300m }
            }
        };

        var dto = engine.Map<OrderSource, OrderDto>(order);
        Assert.NotNull(dto.Tags);
        Assert.Equal(2, dto.Tags.Count); // Set deduplication
        Assert.Contains("Electronics", dto.Tags);

        Assert.NotNull(dto.FeatureDict);
        Assert.True(dto.FeatureDict.ContainsKey("primary"));
        Assert.Equal("Monitor", dto.FeatureDict["primary"].Name);
    }

    [Fact]
    public void Task4_3_NullCollectionHandling_DefaultEmpty_Vs_AllowNull()
    {
        // Default: AllowNullCollections = false -> returns empty collection
        var defaultEngine = new MapEngine(new[] { new Phase4Module() });
        var emptyOrder = new OrderSource { OrderId = 1, Items = null, Tags = null };
        var defaultDto = defaultEngine.Map<OrderSource, OrderDto>(emptyOrder);

        Assert.NotNull(defaultDto.Items);
        Assert.Empty(defaultDto.Items);
        Assert.NotNull(defaultDto.Tags);
        Assert.Empty(defaultDto.Tags);

        // Configured: AllowNullCollections = true -> returns null
        var nullEngine = new MapEngine(new[] { new NullCollectionModule() });
        var nullDto = nullEngine.Map<OrderSource, OrderDto>(emptyOrder);
        Assert.Null(nullDto.Items);
        Assert.Null(nullDto.Tags);
    }

    [Fact]
    public void Task4_4_ExistingCollectionUpdate_PreservesReferenceIdentity()
    {
        var engine = new MapEngine(new[] { new Phase4Module() });
        var existingList = new List<ItemDto>
        {
            new() { Id = 99, Name = "OldItem", Price = 0m }
        };

        var existingDto = new OrderDto
        {
            OrderId = 505,
            Items = existingList
        };

        var source = new OrderSource
        {
            OrderId = 505,
            Items = new List<ItemSource>
            {
                new() { Id = 10, Name = "NewItem1", Price = 100m },
                new() { Id = 20, Name = "NewItem2", Price = 200m }
            }
        };

        engine.Map(source, existingDto);

        // Assert instance reference equality is preserved (critical for ORMs)
        Assert.Same(existingList, existingDto.Items);
        Assert.Equal(2, existingDto.Items.Count);
        Assert.Equal("NewItem1", existingDto.Items[0].Name);
        Assert.Equal("NewItem2", existingDto.Items[1].Name);
    }

    [Fact]
    public void Task4_5_NestedComplexObjects_Chaining()
    {
        var engine = new MapEngine(new[] { new Phase4Module() });
        var org = new OrgSource
        {
            OrgName = "Alphabet Inc.",
            Division = new DivSource
            {
                DivName = "Google DeepMind",
                Department = new DeptSource
                {
                    DeptName = "Advanced Agentic Coding",
                    Manager = new ManagerSource
                    {
                        FullName = "Demis Hassabis",
                        Email = "demis@deepmind.com"
                    }
                }
            }
        };

        var dto = engine.Map<OrgSource, OrgDto>(org);

        Assert.NotNull(dto.Division);
        Assert.Equal("Google DeepMind", dto.Division.DivName);
        Assert.NotNull(dto.Division.Department);
        Assert.Equal("Advanced Agentic Coding", dto.Division.Department.DeptName);
        Assert.NotNull(dto.Division.Department.Manager);
        Assert.Equal("Demis Hassabis", dto.Division.Department.Manager.FullName);
    }

    [Fact]
    public void Task4_6_ConstructUsing_CustomConstructor()
    {
        var engine = new MapEngine(new[] { new Phase4Module() });
        var item = new ItemSource
        {
            Id = 42,
            Name = "quantum-core",
            Price = 1000m
        };

        var target = engine.Map<ItemSource, ImmutableTarget>(item);

        Assert.Equal("QUANTUM-CORE", target.Code);
        Assert.Equal(1100m, target.ScaledValue);
    }

    [Fact]
    public void Task4_7_CircularReference_PreserveReferences()
    {
        var engine = new MapEngine(new[] { new Phase4Module() });

        // Build a circular parent-child graph
        var parent = new NodeSource { Name = "Root" };
        var child = new NodeSource { Name = "Child1", Parent = parent };
        parent.Children.Add(child);

        var parentDto = engine.Map<NodeSource, NodeDto>(parent);

        Assert.NotNull(parentDto);
        Assert.Equal("Root", parentDto.Name);
        Assert.Single(parentDto.Children);
        Assert.Equal("Child1", parentDto.Children[0].Name);

        // Crucial: childDto.Parent must refer to the EXACT SAME instance as parentDto!
        Assert.Same(parentDto, parentDto.Children[0].Parent);
    }
}
