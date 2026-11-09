using System;
using System.Collections.Generic;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class CollectionsController : Controller
{
    private readonly IMapEngine _engine;

    public CollectionsController(IMapEngine engine)
    {
        _engine = engine;
    }

    public IActionResult Standard()
    {
        var order = MockDatabase.GetSampleMultiItemOrder();
        var viewModel = _engine.Map<WebMultiItemOrder, WebMultiItemOrderViewModel>(order);

        ViewBag.SourceOrder = order;
        return View(viewModel);
    }

    public IActionResult DictionariesAndSets()
    {
        var order = MockDatabase.GetSampleMultiItemOrder();
        var viewModel = _engine.Map<WebMultiItemOrder, WebMultiItemOrderViewModel>(order);

        ViewBag.SourceOrder = order;
        return View(viewModel);
    }

    public IActionResult NullCollections()
    {
        var order = MockDatabase.GetSampleNullCollectionOrder();

        // 1. Default engine (AllowNullCollections = false -> empty collections)
        var defaultResult = _engine.Map<WebMultiItemOrder, WebMultiItemOrderViewModel>(order);

        // 2. Custom module with AllowNullCollections = true
        var nullModule = new MappingModuleCollectionTest();
        var nullEngine = new MapEngine(new[] { nullModule });
        var nullResult = nullEngine.Map<WebMultiItemOrder, WebMultiItemOrderViewModel>(order);

        ViewBag.SourceOrder = order;
        ViewBag.DefaultResult = defaultResult;
        ViewBag.NullResult = nullResult;

        return View();
    }

    [HttpGet]
    public IActionResult InPlaceUpdate()
    {
        var existingList = new List<WebOrderItemViewModel>
        {
            new() { Id = 900, ProductName = "Existing Stored Part", UnitPrice = 50.00m, Quantity = 1 }
        };

        var destination = new WebMultiItemOrderViewModel
        {
            OrderId = 7701,
            CustomerName = "SpaceX Launch Systems",
            Items = existingList
        };

        var source = MockDatabase.GetSampleMultiItemOrder();

        // In-place map: updates existingList without reassigning reference
        _engine.Map(source, destination);

        ViewBag.ReferencePreserved = ReferenceEquals(existingList, destination.Items);
        ViewBag.SourceOrder = source;
        return View(destination);
    }

    private class MappingModuleCollectionTest : MappingModule
    {
        public MappingModuleCollectionTest()
        {
            DefineMap<WebOrderItem, WebOrderItemViewModel>();
            DefineMap<WebMultiItemOrder, WebMultiItemOrderViewModel>()
                .AllowNullCollections(true);
        }
    }
}
