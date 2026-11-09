using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class AboutController : Controller
{
    public IActionResult RuntimeMatrix()
    {
        var model = new RuntimeMatrixViewModel
        {
            CurrentFramework = RuntimeInformation.FrameworkDescription,
            ClrVersion = Environment.Version.ToString(),
            OsDescription = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            AutoMapSharpVersion = typeof(MapEngine).Assembly.GetName().Version?.ToString() ?? "1.0.0.0",
            LicenseType = "MIT License (Permissive Cleanroom Open Source)",
            SupportedFrameworks = new List<TfmSupportItem>
            {
                new() { Tfm = "netstandard2.0", Name = ".NET Standard 2.0", Status = "Broad Support", Compatibility = ".NET Framework 4.6.2 - 4.8.1, .NET Core 2.0 - 3.1" },
                new() { Tfm = "net8.0", Name = ".NET 8.0", Status = "LTS (Long Term Support)", Compatibility = "Windows, Linux, macOS. Native AOT for generated maps" },
                new() { Tfm = "net9.0", Name = ".NET 9.0", Status = "STS (Standard Term Support)", Compatibility = "Windows, Linux, macOS. Native AOT for generated maps" }
            }
        };

        return View(model);
    }

    public IActionResult Pipeline()
    {
        var model = new PipelineViewModel
        {
            CiProvider = "GitHub Actions",
            WorkflowFile = ".github/workflows/ci.yml",
            Platforms = new List<string> { "ubuntu-latest (Linux)", "windows-latest (Windows)", "macos-latest (macOS)" },
            VerificationStages = new List<string>
            {
                "1. Multi-SDK Restore (.NET 8.0 & 9.0)",
                "2. Compile Solution with <TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
                "3. Execute 92 Core XUnit Unit Tests across net8.0 & net9.0",
                "4. Compile Roslyn Generators and Diagnostic Analyzers",
                "5. Deterministic NuGet Packaging into artifacts/packages (.nupkg, .snupkg)",
                "6. Fresh Isolated Package Restore & Consumer Execution (80 Tests)",
                "7. Native AOT publish and run of a native binary (Linux)",
                "8. Sample Web Application Compilation with Packed Local NuGet"
            }
        };

        return View(model);
    }
}
