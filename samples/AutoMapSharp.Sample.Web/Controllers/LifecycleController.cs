using System;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class LifecycleController : Controller
{
    private readonly IMapEngine _engine;

    public LifecycleController(IMapEngine engine)
    {
        _engine = engine;
    }

    // Task 5.4: Mapping Lifecycle Callbacks (BeforeMap & AfterMap)
    public IActionResult Hooks()
    {
        var entity = MockDatabase.GetSampleAuditedEntity();
        var viewModel = _engine.Map<WebAuditedEntity, WebAuditedViewModel>(entity);

        ViewBag.SourceEntity = entity;
        return View(viewModel);
    }
}
