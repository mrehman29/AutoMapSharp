using Microsoft.CodeAnalysis;

namespace AutoMapSharp.MigrationAnalyzer;

public static class MigrationDiagnosticDescriptors
{
    private const string MigrationCategory = "Migration";

    public static readonly DiagnosticDescriptor UsingAutoMapper = new(
        id: "AMSMIG001",
        title: "Legacy AutoMapper namespace detected",
        messageFormat: "Replace legacy AutoMapper namespace 'using AutoMapper;' with 'using AutoMapSharp;'",
        category: MigrationCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AutoMapper namespace should be replaced with AutoMapSharp for open-source migration.");

    public static readonly DiagnosticDescriptor InheritsProfile = new(
        id: "AMSMIG002",
        title: "Legacy AutoMapper Profile base class detected",
        messageFormat: "Class '{0}' inherits from legacy 'Profile', replace with 'MappingModule'",
        category: MigrationCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AutoMapper Profile classes should inherit from AutoMapSharp.MappingModule.");

    public static readonly DiagnosticDescriptor CreateMapUsage = new(
        id: "AMSMIG003",
        title: "Legacy CreateMap call detected",
        messageFormat: "Replace legacy 'CreateMap<{0}, {1}>()' with 'DefineMap<{0}, {1}>()'",
        category: MigrationCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "CreateMap should be updated to DefineMap.");

    public static readonly DiagnosticDescriptor ForMemberUsage = new(
        id: "AMSMIG004",
        title: "Legacy ForMember call detected",
        messageFormat: "Replace legacy '.ForMember(...)' with '.ForProperty(...)'",
        category: MigrationCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ForMember should be updated to ForProperty.");

    public static readonly DiagnosticDescriptor MapFromUsage = new(
        id: "AMSMIG005",
        title: "Legacy MapFrom option detected",
        messageFormat: "Replace legacy '.MapFrom(...)' with '.MapSource(...)'",
        category: MigrationCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "MapFrom option should be updated to MapSource.");

    public static readonly DiagnosticDescriptor IgnoreUsage = new(
        id: "AMSMIG006",
        title: "Legacy Ignore option detected",
        messageFormat: "Replace legacy '.Ignore()' with '.Skip()'",
        category: MigrationCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Ignore option should be updated to Skip.");

    public static readonly DiagnosticDescriptor ProjectToUsage = new(
        id: "AMSMIG007",
        title: "Legacy ProjectTo query projection detected",
        messageFormat: "Replace legacy '.ProjectTo<{0}>(...)' with '.ProjectOnto<{0}>(...)'",
        category: MigrationCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ProjectTo LINQ extension should be updated to ProjectOnto.");

    public static readonly DiagnosticDescriptor IMapperUsage = new(
        id: "AMSMIG008",
        title: "Legacy IMapper interface detected",
        messageFormat: "Replace legacy 'IMapper' with 'IMapEngine'",
        category: MigrationCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "IMapper interface should be updated to IMapEngine.");
}
