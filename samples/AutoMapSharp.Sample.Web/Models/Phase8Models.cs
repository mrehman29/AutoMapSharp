using System.Linq;
using System;
using System.Collections.Generic;

namespace AutoMapSharp.Sample.Web.Models;

public class MeasuredScenario
{
    public string Scenario { get; set; } = "";
    public string Measured { get; set; } = "";
    public string PreviouslyClaimed { get; set; } = "";
}

/// <summary>
/// Honest benchmark status. Numbers come from the 2026-11-07 code review, not from marketing copy.
/// </summary>
public class BenchmarkStatusViewModel
{
    public string Status { get; set; } = "";
    public string Method { get; set; } = "";
    public List<MeasuredScenario> Measurements { get; set; } = new();
    public List<string> NextSteps { get; set; } = new();
}

/// <summary>
/// Live runtime facts plus the current Native AOT support status of AutoMapSharp.
/// </summary>
public class NativeAotStatusViewModel
{
    public string DotNetVersion { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public bool IsDynamicCodeSupported { get; set; }
    public bool IsDynamicCodeCompiled { get; set; }
    public bool AutoMapSharpSupportsNativeAot { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<PlanRow> Plans { get; set; } = new();
    public int GeneratedCount => Plans.Count(p => p.Kind == "Generated");
}

public class RuntimeMatrixViewModel
{
    public string CurrentFramework { get; set; } = "";
    public string ClrVersion { get; set; } = "";
    public string OsDescription { get; set; } = "";
    public string ProcessArchitecture { get; set; } = "";
    public string AutoMapSharpVersion { get; set; } = "1.0.0-preview.1";
    public string LicenseType { get; set; } = "MIT License (100% Permissive Open Source)";
    public List<TfmSupportItem> SupportedFrameworks { get; set; } = new();
}

public class TfmSupportItem
{
    public string Tfm { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Compatibility { get; set; } = "";
}

public class PipelineViewModel
{
    public string CiProvider { get; set; } = "GitHub Actions";
    public string WorkflowFile { get; set; } = ".github/workflows/ci.yml";
    public List<string> Platforms { get; set; } = new() { "ubuntu-latest", "windows-latest", "macos-latest" };
    public List<string> VerificationStages { get; set; } = new();
}

public class PlanRow
{
    public string Map { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Reason { get; set; }
}
