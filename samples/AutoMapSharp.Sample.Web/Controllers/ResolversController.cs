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

    public IActionResult Index()
    {
        var source = MockDatabase.GetSampleOrderTaxSource();
        var viewModel = _engine.Map<WebOrderTaxSource, WebOrderTaxViewModel>(source);

        ViewBag.SourceOrder = source;
        return View(viewModel);
    }

    public IActionResult TypeConverters()
    {
        var rawSource = MockDatabase.GetSampleRawJsonSource();
        var viewModel = _engine.Map<WebRawJsonSource, WebParsedJsonViewModel>(rawSource);

        ViewBag.RawSource = rawSource;
        return View(viewModel);
    }
}
