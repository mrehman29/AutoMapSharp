using System.Collections.Generic;

namespace AutoMapSharp.Sample.Web.Models;

public class AnalyzerRuleViewModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string BadCodeExample { get; set; } = "";
    public string FixedCodeExample { get; set; } = "";
}

public class CodeFixViewModel
{
    public string RuleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string TriggerDescription { get; set; } = "";
    public string BeforeCode { get; set; } = "";
    public string AfterCode { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public class MigrationScannerViewModel
{
    public string InputCode { get; set; } = "";
    public List<MigrationDiagnosticItem> DetectedDiagnostics { get; set; } = new();
    public bool HasRun { get; set; }
}

public class MigrationDiagnosticItem
{
    public string RuleId { get; set; } = "";
    public int LineNumber { get; set; }
    public string Description { get; set; } = "";
}

public class MigrationPlaygroundViewModel
{
    public string OriginalCode { get; set; } = "";
    public string MigratedCode { get; set; } = "";
    public List<MigrationDiagnosticItem> ChangesApplied { get; set; } = new();
    public int TotalReplacements { get; set; }
    public bool WasMigrated { get; set; }
}
