using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapSharp.MigrationAnalyzer;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class MigrationController : Controller
{
    private const string DefaultAutoMapperSample = @"using System;
using AutoMapper;
using AutoMapper.QueryableExtensions;

namespace LegacyApp.Mapping;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<OrderEntity, OrderDto>()
            .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer.FullName))
            .ForMember(dest => dest.InternalNotes, opt => opt.Ignore())
            .ForMember(dest => dest.Discount, opt => opt.Condition(src => src.IsMember))
            .ForMember(dest => dest.Notes, opt => opt.NullSubstitute(""(none)""))
            .ForMember(dest => dest.Tax, opt => opt.MapFrom<TaxResolver>())
            .ReverseMap();

        CreateMap<OrderItemEntity, OrderItemDto>();
    }

    public IQueryable<OrderDto> Query(IQueryable<OrderEntity> orders, IMapper mapper) =>
        orders.ProjectTo<OrderDto>(mapper.ConfigurationProvider);
}

public class TaxResolver : IValueResolver<OrderEntity, OrderDto, decimal>
{
    public decimal Resolve(OrderEntity source, OrderDto destination, decimal destMember, ResolutionContext context) => source.Total * 0.2m;
}";

    public IActionResult Analyzers()
    {
        var rules = new List<AnalyzerRuleViewModel>
        {
            new()
            {
                Id = "AMS001",
                Title = "Unmapped Destination Property",
                Severity = "Warning",
                Category = "Usage",
                Description = "Triggered when a destination property has no matching source property (and cannot be flattened) and is not configured with .Skip().",
                BadCodeExample = @"DefineMap<User, UserDto>(); // UserDto has 'SecurityPin' which does not exist on User",
                FixedCodeExample = @"DefineMap<User, UserDto>()
    .ForProperty(dest => dest.SecurityPin, opt => opt.Skip());"
            },
            new()
            {
                Id = "AMS002",
                Title = "Incompatible Property Types",
                Severity = "Error",
                Category = "Usage",
                Description = "Triggered when source and destination properties share the same name but have incompatible types (e.g. DateTime to int) with no converter.",
                BadCodeExample = @"DefineMap<AuditLog, AuditDto>(); // Log.Timestamp is DateTime, AuditDto.Timestamp is int",
                FixedCodeExample = @"DefineMap<AuditLog, AuditDto>()
    .ForProperty(dest => dest.Timestamp, opt => opt.MapSource(src => (int)src.Timestamp.Ticks));"
            },
            new()
            {
                Id = "AMS003",
                Title = "Potential Circular Reference Without Depth Controls",
                Severity = "Warning",
                Category = "Design",
                Description = "Triggered when a mapped entity directly or indirectly references itself, risking recursive StackOverflowException during mapping.",
                BadCodeExample = @"DefineMap<Employee, EmployeeDto>(); // Employee has Employee Manager property",
                FixedCodeExample = @"DefineMap<Employee, EmployeeDto>()
    .MaxDepth(3)
    .PreserveReferences();"
            }
        };

        return View(rules);
    }

    public IActionResult CodeFixes()
    {
        var fixes = new List<CodeFixViewModel>
        {
            new()
            {
                RuleId = "AMS001",
                Title = "Ignore property using .Skip()",
                TriggerDescription = "Destination property 'SecretCode' has no matching source field.",
                BeforeCode = @"public class UserModule : MappingModule
{
    public UserModule()
    {
        DefineMap<User, UserDto>();
    }
}",
                AfterCode = @"public class UserModule : MappingModule
{
    public UserModule()
    {
        DefineMap<User, UserDto>()
            .ForProperty(dest => dest.SecretCode, opt => opt.Skip());
    }
}",
                Explanation = "Pressing Ctrl+. or Alt+Enter on the AMS001 warning applies this lightbulb fix automatically."
            },
            new()
            {
                RuleId = "AMS001",
                Title = "Map property using .MapSource()",
                TriggerDescription = "Destination property 'ExternalReference' exists under a different source name.",
                BeforeCode = @"public class OrderModule : MappingModule
{
    public OrderModule()
    {
        DefineMap<Order, OrderDto>();
    }
}",
                AfterCode = @"public class OrderModule : MappingModule
{
    public OrderModule()
    {
        DefineMap<Order, OrderDto>()
            .ForProperty(dest => dest.ExternalReference, opt => opt.MapSource(src => src.ExternalReference));
    }
}",
                Explanation = "Generates a stub lambda mapping expression so you can specify the target source expression."
            }
        };

        return View(fixes);
    }

    [HttpGet]
    public IActionResult AutoMapperScanner()
    {
        var model = new MigrationScannerViewModel
        {
            InputCode = DefaultAutoMapperSample,
            HasRun = false
        };
        return View(model);
    }

    [HttpPost]
    public IActionResult AutoMapperScanner(MigrationScannerViewModel model)
    {
        string code = model.InputCode ?? string.Empty;
        var report = AutoMapperMigrator.MigrateSourceCode(code);

        model.DetectedDiagnostics = report.Changes.Select(c => new MigrationDiagnosticItem
        {
            RuleId = c.RuleId,
            LineNumber = c.LineNumber,
            Description = c.Description
        }).ToList();

        model.HasRun = true;
        return View(model);
    }

    [HttpGet]
    public IActionResult Playground()
    {
        var model = new MigrationPlaygroundViewModel
        {
            OriginalCode = DefaultAutoMapperSample
        };

        var report = AutoMapperMigrator.MigrateSourceCode(model.OriginalCode);
        model.MigratedCode = report.MigratedCode;
        model.TotalReplacements = report.TotalReplacements;
        model.WasMigrated = report.WasModified;
        model.ChangesApplied = report.Changes.Select(c => new MigrationDiagnosticItem
        {
            RuleId = c.RuleId,
            LineNumber = c.LineNumber,
            Description = c.Description
        }).ToList();

        return View(model);
    }

    [HttpPost]
    public IActionResult Playground(MigrationPlaygroundViewModel model)
    {
        string code = model.OriginalCode ?? string.Empty;
        var report = AutoMapperMigrator.MigrateSourceCode(code);

        model.MigratedCode = report.MigratedCode;
        model.TotalReplacements = report.TotalReplacements;
        model.WasMigrated = report.WasModified;
        model.ChangesApplied = report.Changes.Select(c => new MigrationDiagnosticItem
        {
            RuleId = c.RuleId,
            LineNumber = c.LineNumber,
            Description = c.Description
        }).ToList();

        return View(model);
    }
}
