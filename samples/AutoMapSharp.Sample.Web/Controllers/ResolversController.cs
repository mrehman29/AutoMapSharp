using System;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class ResolversController : Controller
{
    private readonly IMapEngine _engine;

    public ResolversController(IMapEngine engine)
    {
        _engine = engine;
    }

    // Task 5.1: Custom Property Resolvers with DI
    public IActionResult Index()
    {
        var source = MockDatabase.GetSampleOrderTaxSource();
        var viewModel = _engine.Map<WebOrderTaxSource, WebOrderTaxViewModel>(source);

        ViewBag.SourceOrder = source;
        return View(viewModel);
    }

    // Task 5.2: Type-Level Custom Converters (ConvertUsing)
    public IActionResult TypeConverters()
    {
        var rawSource = MockDatabase.GetSampleRawJsonSource();
        var viewModel = _engine.Map<WebRawJsonSource, WebParsedJsonViewModel>(rawSource);

        ViewBag.RawSource = rawSource;
        return View(viewModel);
    }
}
