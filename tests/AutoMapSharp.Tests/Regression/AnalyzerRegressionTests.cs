using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapSharp.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

/// <summary>
/// F27: the AMS001 analyzer used its own name normalization, stripping endings like Dto and Entity, so it disagreed
/// with the runtime. It now uses the runtime's matching rules and reads the module's naming settings.
/// </summary>
public class AnalyzerRegressionTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string code)
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(MappingModule).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        var compilation = CSharpCompilation.Create("F27", new[] { CSharpSyntaxTree.ParseText(code) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MappingConfigurationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    [Fact]
    [Trait("Finding", "F27")]
    public async Task F27_NameThatOnlyDiffersByDtoSuffix_IsReported_LikeTheRuntime()
    {
        const string code = @"
using AutoMapSharp;
public class Source { public string Customer { get; set; } = """"; }
public class Target { public string CustomerDto { get; set; } = """"; }
public class M : MappingModule { public M() { DefineMap<Source, Target>(); } }";

        var diagnostics = await AnalyzeAsync(code);

        Assert.Contains(diagnostics, d => d.Id == "AMS001" && d.GetMessage().Contains("CustomerDto"));
    }

    public class Source { public string Customer { get; set; } = ""; }
    public class Target { public string CustomerDto { get; set; } = ""; }
    public class RuntimeModule : MappingModule { public RuntimeModule() { DefineMap<Source, Target>(); } }

    [Fact]
    [Trait("Finding", "F27")]
    public void F27_RuntimeValidation_AgreesWithAnalyzer()
    {
        var error = Assert.Single(new MapEngine(new RuntimeModule()).ValidateConfiguration().Errors);
        Assert.Contains("CustomerDto", error.UnmappedProperties);
    }

    [Fact]
    [Trait("Finding", "F27")]
    public async Task F27_ModuleNamingSettings_AreRespected()
    {
        const string code = @"
using AutoMapSharp;
using AutoMapSharp.Conventions;
public class Legacy { public string m_Title { get; set; } = """"; public string CategoryModel { get; set; } = """"; public string first_name { get; set; } = """"; public string Code = """"; }
public class Clean { public string Title { get; set; } = """"; public string Category { get; set; } = """"; public string FirstName { get; set; } = """"; public string Code { get; set; } = """"; }
public class M : MappingModule
{
    public M()
    {
        RecognizePrefixes(""m_"");
        RecognizeSuffixes(""Model"");
        SourceNamingConvention = SnakeCaseNamingConvention.Instance;
        DefineMap<Legacy, Clean>();
    }
}";

        var diagnostics = await AnalyzeAsync(code);

        Assert.DoesNotContain(diagnostics, d => d.Id == "AMS001");
    }

    [Fact]
    [Trait("Finding", "F27")]
    public async Task F27_DestinationPrefixesAndReplacements_AreRespected()
    {
        const string code = @"
using AutoMapSharp;
public class Row { public string Name { get; set; } = """"; public string Straße { get; set; } = """"; }
public class View { public string DtoName { get; set; } = """"; public string Strasse { get; set; } = """"; }
public class M : MappingModule
{
    public M()
    {
        RecognizeDestinationPrefixes(""Dto"");
        ReplaceMemberName(""ß"", ""ss"");
        DefineMap<Row, View>();
    }
}";

        var diagnostics = await AnalyzeAsync(code);

        Assert.DoesNotContain(diagnostics, d => d.Id == "AMS001");
    }

    [Fact]
    [Trait("Finding", "F27")]
    public async Task F27_ForCtorParamAndForPath_CountAsMapped_LikeTheRuntime()
    {
        const string code = @"
using AutoMapSharp;
public class Item { public string Code { get; set; } = """"; public string City { get; set; } = """"; }
public record Label(string Text, int Copies);
public class Address { public string Town { get; set; } = """"; }
public class Shipment { public Address? Destination { get; set; } }
public class M : MappingModule
{
    public M()
    {
        DefineMap<Item, Label>()
            .ForCtorParam(""text"", o => o.MapSource(s => s.Code))
            .ForCtorParam(""copies"", o => o.UseValue(1));
        DefineMap<Item, Shipment>()
            .ForPath(d => d.Destination!.Town, o => o.MapSource(s => s.City));
    }
}";

        var diagnostics = await AnalyzeAsync(code);

        Assert.DoesNotContain(diagnostics, d => d.Id == "AMS001");
    }

    [Fact]
    [Trait("Finding", "F27")]
    public async Task F27_ExtensionMethodsAttributesAndDictionaries_AgreeWithRuntime()
    {
        const string code = @"
using System.Collections.Generic;
using AutoMapSharp;
public class Customer { public List<int> Orders { get; set; } = new(); }
public static class CustomerExtensions { public static int OrderCount(this Customer c) => c.Orders.Count; }
public class Summary
{
    public int OrderCount { get; set; }
    [SkipMap] public string Internal { get; set; } = """";
    [SourceMember(""Orders"")] public List<int> Ids { get; set; } = new();
}
public class Person { public string Name { get; set; } = """"; }
public class M : MappingModule
{
    public M()
    {
        IncludeSourceExtensionMethods(typeof(CustomerExtensions));
        DefineMap<Customer, Summary>();
        DefineMap<Dictionary<string, object>, Person>();
    }
}";

        var diagnostics = await AnalyzeAsync(code);

        Assert.DoesNotContain(diagnostics, d => d.Id == "AMS001");
    }
}
