using System;
using AutoMapSharp;
using AutoMapSharp.Validation;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class DiagnosticsController : Controller
{
    private readonly IMapEngine _engine;

    public DiagnosticsController(IMapEngine engine)
    {
        _engine = engine;
    }

    // Task 5.5: Configuration Validation (AssertConfigurationIsValid & ValidateConfiguration)
    public IActionResult HealthCheck()
    {
        var validation = _engine.ValidateConfiguration();
        return View(validation);
    }
}
