using Microsoft.CodeAnalysis;

namespace AutoMapSharp.Analyzers;

public static class DiagnosticDescriptors
{
    private const string UsageCategory = "Usage";
    private const string DesignCategory = "Design";

    public static readonly DiagnosticDescriptor UnmappedDestinationProperty = new(
        id: "AMS001",
        title: "Unmapped destination property",
        messageFormat: "Destination property '{0}' on type '{1}' is not mapped from source '{2}' and is not configured with Skip()",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All destination properties should be explicitly mapped or skipped to avoid unexpected default values.");

    public static readonly DiagnosticDescriptor IncompatiblePropertyTypes = new(
        id: "AMS002",
        title: "Incompatible property types",
        messageFormat: "Property '{0}' type '{1}' on source '{2}' is incompatible with destination property type '{3}' on '{4}'",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Source and destination property types must be compatible or explicitly converted.");

    public static readonly DiagnosticDescriptor PotentialCircularReference = new(
        id: "AMS003",
        title: "Potential circular reference detected",
        messageFormat: "Type '{0}' contains a recursive/circular reference to itself or '{1}', but neither MaxDepth() nor PreserveReferences() is configured",
        category: DesignCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Self-referencing or cyclic relational object graphs require MaxDepth or PreserveReferences to avoid recursion.");
}
