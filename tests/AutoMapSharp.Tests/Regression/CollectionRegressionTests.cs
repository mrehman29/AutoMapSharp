using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class CollectionRegressionTests
{
    public class Source { public List<int> Items { get; set; } = new() { 3, 1, 2 }; }
    public class Destination<TCollection> { public TCollection Items { get; set; } = default!; }

    public class DictionarySource { public Dictionary<string, int> Items { get; set; } = new() { ["b"] = 2, ["a"] = 1 }; }
    public class DictionaryDestination<TDictionary> { public TDictionary Items { get; set; } = default!; }

    public class LeafSource { public Uri? Link { get; set; } public byte[]? Data { get; set; } }
    public class LeafDestination { public Uri? Link { get; set; } public byte[]? Data { get; set; } }

    public class CollectionModule : MappingModule
    {
        public CollectionModule()
        {
            DefineMap<Source, Destination<List<int>>>();
            DefineMap<Source, Destination<ObservableCollection<int>>>();
            DefineMap<Source, Destination<Collection<int>>>();
            DefineMap<Source, Destination<ReadOnlyCollection<int>>>();
            DefineMap<Source, Destination<IReadOnlyList<int>>>();
            DefineMap<Source, Destination<IReadOnlySet<int>>>();
            DefineMap<Source, Destination<SortedSet<int>>>();
            DefineMap<Source, Destination<ImmutableList<int>>>();
            DefineMap<Source, Destination<ImmutableArray<int>>>();
            DefineMap<Source, Destination<LinkedList<int>>>();
            DefineMap<Source, Destination<Queue<int>>>();
            DefineMap<Source, Destination<Stack<int>>>();
            DefineMap<DictionarySource, DictionaryDestination<SortedDictionary<string, int>>>();
            DefineMap<DictionarySource, DictionaryDestination<ConcurrentDictionary<string, int>>>();
            DefineMap<LeafSource, LeafDestination>();
        }
    }

    private static readonly IMapEngine Engine = new MapEngine(new CollectionModule());

    private static void AssertOrdered<TCollection>() where TCollection : IEnumerable<int>
    {
        var result = Engine.Map<Source, Destination<TCollection>>(new Source());
        Assert.NotNull(result.Items);
        Assert.IsAssignableFrom<TCollection>(result.Items);
        Assert.Equal(new[] { 3, 1, 2 }, result.Items);
    }

    private static void AssertSameItems<TCollection>() where TCollection : IEnumerable<int>
    {
        var result = Engine.Map<Source, Destination<TCollection>>(new Source());
        Assert.NotNull(result.Items);
        Assert.IsAssignableFrom<TCollection>(result.Items);
        Assert.Equal(new[] { 1, 2, 3 }, result.Items.OrderBy(x => x));
    }

    [Fact, Trait("Finding", "F13")] public void F13_List() => AssertOrdered<List<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_ObservableCollection() => AssertOrdered<ObservableCollection<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_Collection() => AssertOrdered<Collection<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_ReadOnlyCollection() => AssertOrdered<ReadOnlyCollection<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_IReadOnlyList() => AssertOrdered<IReadOnlyList<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_IReadOnlySet() => AssertSameItems<IReadOnlySet<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_SortedSet() => AssertSameItems<SortedSet<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_ImmutableList() => AssertOrdered<ImmutableList<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_ImmutableArray() => AssertOrdered<ImmutableArray<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_LinkedList() => AssertOrdered<LinkedList<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_Queue() => AssertOrdered<Queue<int>>();
    [Fact, Trait("Finding", "F13")] public void F13_Stack() => AssertSameItems<Stack<int>>();

    [Fact, Trait("Finding", "F13")]
    public void F13_SortedDictionary()
    {
        var result = Engine.Map<DictionarySource, DictionaryDestination<SortedDictionary<string, int>>>(new DictionarySource());
        Assert.IsType<SortedDictionary<string, int>>(result.Items);
        Assert.Equal(new[] { "a", "b" }, result.Items.Keys);
    }

    [Fact, Trait("Finding", "F13")]
    public void F13_ConcurrentDictionary()
    {
        var result = Engine.Map<DictionarySource, DictionaryDestination<ConcurrentDictionary<string, int>>>(new DictionarySource());
        Assert.IsType<ConcurrentDictionary<string, int>>(result.Items);
        Assert.Equal(2, result.Items["b"]);
    }

    [Fact, Trait("Finding", "F13")]
    public void F13_UriAndByteArray_ArePreserved()
    {
        var source = new LeafSource { Link = new Uri("https://example.com/a?b=1"), Data = new byte[] { 1, 2, 3 } };

        var result = Engine.Map<LeafSource, LeafDestination>(source);

        Assert.Equal(source.Link, result.Link);
        Assert.Equal(source.Data, result.Data);
    }
}
