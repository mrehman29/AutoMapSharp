using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapSharp.Analyzers;
using AutoMapSharp.MigrationAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AutoMapSharp.Tests;

public class Phase7Tests
{
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        DiagnosticAnalyzer analyzer,
        string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(MappingModule).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(analyzer));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AMS001_EmitsDiagnostic_WhenDestinationPropertyIsUnmapped()
    {
        string code = @"
using AutoMapSharp;

public class UserSource
{
    public string Name { get; set; }
}

public class UserDto
{
    public string Name { get; set; }
    public string SecretCode { get; set; }
}

public class TestModule : MappingModule
{
    public TestModule()
    {
        DefineMap<UserSource, UserDto>();
    }
}";

        var diagnostics = await RunAnalyzerAsync(new MappingConfigurationAnalyzer(), code);
        var ams001 = diagnostics.FirstOrDefault(d => d.Id == "AMS001");

        Assert.NotNull(ams001);
        Assert.Contains("SecretCode", ams001.GetMessage());
    }

    [Fact]
    public async Task AMS001_DoesNotEmit_WhenUnmappedPropertyIsSkipped()
    {
        string code = @"
using AutoMapSharp;

public class UserSource
{
    public string Name { get; set; }
}

public class UserDto
{
    public string Name { get; set; }
    public string SecretCode { get; set; }
}

public class TestModule : MappingModule
{
    public TestModule()
    {
        DefineMap<UserSource, UserDto>()
            .ForProperty(dest => dest.SecretCode, opt => opt.Skip());
    }
}";

        var diagnostics = await RunAnalyzerAsync(new MappingConfigurationAnalyzer(), code);
        var ams001 = diagnostics.FirstOrDefault(d => d.Id == "AMS001");

        Assert.Null(ams001);
    }

    [Fact]
    public async Task AMS002_DoesNotEmit_ForEnumToEnumOrUserStructs()
    {
        string code = @"
using AutoMapSharp;

public enum SourceLevel { Low, High }
public enum TargetLevel { High, Low }
public class PriceSource { public decimal Amount { get; set; } }
public struct Money { public decimal Amount { get; set; } }

public class Source { public SourceLevel Level { get; set; } public PriceSource Price { get; set; } }
public class Target { public TargetLevel Level { get; set; } public Money Price { get; set; } }

public class TestModule : MappingModule
{
    public TestModule()
    {
        DefineMap<Source, Target>();
    }
}";

        var diagnostics = await RunAnalyzerAsync(new MappingConfigurationAnalyzer(), code);

        Assert.DoesNotContain(diagnostics, d => d.Id == "AMS002");
    }

    [Fact]
    public async Task AMS002_EmitsDiagnostic_WhenPropertyTypesAreIncompatible()
    {
        string code = @"
using System;
using AutoMapSharp;

public class DateSource
{
    public DateTime Timestamp { get; set; }
}

public class IntDto
{
    public int Timestamp { get; set; }
}

public class TestModule : MappingModule
{
    public TestModule()
    {
        DefineMap<DateSource, IntDto>();
    }
}";

        var diagnostics = await RunAnalyzerAsync(new MappingConfigurationAnalyzer(), code);
        var ams002 = diagnostics.FirstOrDefault(d => d.Id == "AMS002");

        Assert.NotNull(ams002);
        Assert.Contains("Timestamp", ams002.GetMessage());
    }

    [Fact]
    public async Task AMS003_EmitsDiagnostic_WhenRecursiveGraphHasNoMaxDepthOrPreserveReferences()
    {
        string code = @"
using AutoMapSharp;

public class EmployeeNode
{
    public string Name { get; set; }
    public EmployeeNode Manager { get; set; }
}

public class EmployeeNodeDto
{
    public string Name { get; set; }
    public EmployeeNodeDto Manager { get; set; }
}

public class TestModule : MappingModule
{
    public TestModule()
    {
        DefineMap<EmployeeNode, EmployeeNodeDto>();
    }
}";

        var diagnostics = await RunAnalyzerAsync(new MappingConfigurationAnalyzer(), code);
        var ams003 = diagnostics.FirstOrDefault(d => d.Id == "AMS003");

        Assert.NotNull(ams003);
    }

    [Fact]
    public async Task AMS003_DoesNotEmit_WhenMaxDepthIsConfigured()
    {
        string code = @"
using AutoMapSharp;

public class EmployeeNode
{
    public string Name { get; set; }
    public EmployeeNode Manager { get; set; }
}

public class EmployeeNodeDto
{
    public string Name { get; set; }
    public EmployeeNodeDto Manager { get; set; }
}

public class TestModule : MappingModule
{
    public TestModule()
    {
        DefineMap<EmployeeNode, EmployeeNodeDto>()
            .MaxDepth(3);
    }
}";

        var diagnostics = await RunAnalyzerAsync(new MappingConfigurationAnalyzer(), code);
        var ams003 = diagnostics.FirstOrDefault(d => d.Id == "AMS003");

        Assert.Null(ams003);
    }

    [Fact]
    public async Task MigrationAnalyzer_DetectsAllLegacyAutoMapperConstructs()
    {
        string legacyCode = @"
using AutoMapper;
using AutoMapper.QueryableExtensions;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName, opt => opt.MapFrom(s => s.ClientName))
            .ForMember(d => d.InternalNotes, opt => opt.Ignore());
    }

    public void Query(IMapper mapper)
    {
    }
}";

        var diagnostics = await RunAnalyzerAsync(new AutoMapperMigrationAnalyzer(), legacyCode);

        Assert.Contains(diagnostics, d => d.Id == "AMSMIG001");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG002");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG003");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG004");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG005");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG006");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG008");
    }

    [Fact]
    public void AutoMapperMigrator_TransformsLegacyProfileToAutoMapSharp()
    {
        string legacyCode = @"using AutoMapper;

public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customer, CustomerDto>()
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Secret, opt => opt.Ignore());
    }
}";

        var report = AutoMapperMigrator.MigrateSourceCode(legacyCode);

        Assert.True(report.WasModified);
        Assert.True(report.TotalReplacements >= 5);
        Assert.Contains("using AutoMapSharp;", report.MigratedCode);
        Assert.Contains("class CustomerProfile : MappingModule", report.MigratedCode);
        Assert.Contains("DefineMap<Customer, CustomerDto>()", report.MigratedCode);
        Assert.Contains(".ForProperty(dest => dest.FullName, opt => opt.MapSource(src => src.Name))", report.MigratedCode);
        Assert.Contains(".ForProperty(dest => dest.Secret, opt => opt.Skip())", report.MigratedCode);
    }
}
