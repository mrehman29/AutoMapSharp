using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using AutoMapSharp;
using AutoMapSharp.Exceptions;
using Xunit;

namespace AutoMapSharp.IntegrationTests.NugetConsumer;

/// <summary>
/// Verifies the remediation Phase 11 compiled engine through the packed NuGet package.
/// </summary>
public class RemediationPhase11ConsumerTests
{
    public enum SourceLevel { Gold, Silver, Bronze }
    public enum TargetLevel { Bronze, Silver, Gold }

    public class LineEntity { public string Sku { get; set; } = ""; public int Quantity { get; set; } }
    public class Buyer { public string Name { get; set; } = ""; public SourceLevel Level { get; set; } }

    public class InvoiceEntity
    {
        public int Number { get; set; }
        public Buyer? Buyer { get; set; }
        public List<LineEntity> Lines { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    public record LineRecord(string Sku, int Quantity);
    public record InvoiceRecord(int Number, string BuyerName, TargetLevel BuyerLevel, IReadOnlyList<LineRecord> Lines);

    public class InvoiceView
    {
        public int Number { get; set; }
        public string BuyerName { get; set; } = "";
        public ObservableCollection<LineRecord> Lines { get; set; } = new();
        public ImmutableList<string> Tags { get; set; } = ImmutableList<string>.Empty;
    }

    public class InvoiceModule : MappingModule
    {
        public InvoiceModule()
        {
            DefineMap<LineEntity, LineRecord>();
            DefineMap<InvoiceEntity, InvoiceRecord>();
            DefineMap<InvoiceEntity, InvoiceView>().ReverseMap();
        }
    }

    private static InvoiceEntity Sample() => new()
    {
        Number = 77,
        Buyer = new Buyer { Name = "Ann", Level = SourceLevel.Gold },
        Lines = { new() { Sku = "A", Quantity = 2 }, new() { Sku = "B", Quantity = 1 } },
        Tags = { "paid", "eu" }
    };

    [Fact]
    public void Package_MapsRecordsWithFlatteningEnumsByNameAndNestedCollections()
    {
        var engine = new MapEngine(new InvoiceModule());

        var record = engine.Map<InvoiceEntity, InvoiceRecord>(Sample());

        Assert.Equal(77, record.Number);
        Assert.Equal("Ann", record.BuyerName);
        Assert.Equal(TargetLevel.Gold, record.BuyerLevel);
        Assert.Equal(new[] { "A", "B" }, record.Lines.Select(l => l.Sku));
    }

    [Fact]
    public void Package_MapsObservableAndImmutableCollections()
    {
        var engine = new MapEngine(new InvoiceModule());

        var view = engine.Map<InvoiceEntity, InvoiceView>(Sample());

        Assert.Equal(2, view.Lines.Count);
        Assert.Equal(new[] { "paid", "eu" }, view.Tags);
    }

    [Fact]
    public void Package_ReverseMapUnflattensIntoNestedObject()
    {
        var engine = new MapEngine(new InvoiceModule());

        var entity = engine.Map<InvoiceView, InvoiceEntity>(new InvoiceView { Number = 5, BuyerName = "Bo" });

        Assert.Equal(5, entity.Number);
        Assert.Equal("Bo", entity.Buyer!.Name);
    }

    public class OtherInvoiceModule : MappingModule
    {
        public OtherInvoiceModule() { DefineMap<LineEntity, LineRecord>(); }
    }

    [Fact]
    public void Package_DuplicateMapsAcrossModules_AreRejected()
    {
        var ex = Assert.Throws<AutoMapSharpConfigurationException>(() => new MapEngine(new InvoiceModule(), new OtherInvoiceModule()));

        Assert.Contains(nameof(OtherInvoiceModule), ex.Message);
    }
}
