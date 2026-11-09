using System.Collections.Generic;
using System.Linq;
using AutoMapSharp;
using AutoMapSharp.Configuration;
using Xunit;

namespace AutoMapSharp.IntegrationTests.NugetConsumer;

/// <summary>
/// Checks AutoMapper parity features through the packed NuGet package.
/// </summary>
public class RemediationPhase13ConsumerTests
{
    public class Item { public string Code = "A1"; public int Quantity { get; set; } = 2; public long PriceCents { get; set; } = 1999; }
    public class ItemDto { public string Code = ""; public int Quantity { get; set; } public string Price { get; set; } = ""; }

    public class CentsConverter : IValueConverter<long, string>
    {
        public string Convert(long sourceMember, ResolutionContext context) => (sourceMember / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + context.Items["suffix"];
    }

    public class Page<T> { public List<T> Items { get; set; } = new(); public int Total { get; set; } }
    public class PageDto<T> { public List<T> Items { get; set; } = new(); public int Count { get; set; } }

    public record Label(string Text, int Copies);

    [Fact]
    public void Package_InlineConfiguration_FieldsValueConvertersAndItems()
    {
        var engine = new MapEngine(cfg =>
            cfg.DefineMap<Item, ItemDto>().ForProperty(d => d.Price, o => o.ConvertUsing<CentsConverter, long>(s => s.PriceCents)));

        var dto = engine.Map<Item, ItemDto>(new Item(), opt => opt.Items["suffix"] = " EUR");

        Assert.Equal("A1", dto.Code);
        Assert.Equal(2, dto.Quantity);
        Assert.Equal("19.99 EUR", dto.Price);
    }

    public class GenericModule : MappingModule
    {
        public GenericModule()
        {
            DefineMap(typeof(Page<>), typeof(PageDto<>)).ForProperty("Count", o => o.MapSource("Total"));
            DefineMap<Item, Label>()
                .ForCtorParam("text", o => o.MapSource(s => "#" + s.Code))
                .ForCtorParam("copies", o => o.UseValue(3));
        }
    }

    [Fact]
    public void Package_OpenGenericsAndForCtorParam()
    {
        var engine = new MapEngine(new GenericModule());

        var page = engine.Map<Page<int>, PageDto<int>>(new Page<int> { Items = { 1, 2, 3 }, Total = 3 });
        var label = engine.Map<Item, Label>(new Item());

        Assert.Equal(3, page.Count);
        Assert.Equal(new[] { 1, 2, 3 }, page.Items);
        Assert.Equal(new Label("#A1", 3), label);
        engine.AssertConfigurationIsValid();
    }

    public class Wide { public string Name { get; set; } = ""; public string Unused { get; set; } = ""; }
    public class Narrow { public string Name { get; set; } = ""; }

    [Fact]
    public void Package_ValidationWithMemberListSource()
    {
        var config = new MappingConfiguration(cfg => cfg.DefineMap<Wide, Narrow>().ValidateMembers(MemberList.Source));

        var error = Assert.Single(config.ValidateConfiguration().Errors);

        Assert.Equal(new[] { "Unused" }, error.UnmappedSourceMembers);
    }
}
