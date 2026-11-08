using System;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class ComplexTypesController : Controller
{
    private readonly IMapEngine _engine;

    public ComplexTypesController(IMapEngine engine)
    {
        _engine = engine;
    }

    // Task 4.5: Nested Complex Objects & Sub-Mapper Chaining
    public IActionResult DeepChaining()
    {
        var hierarchy = MockDatabase.GetSampleHierarchy();
        var viewModel = _engine.Map<WebOrgEnterprise, WebOrgEnterpriseViewModel>(hierarchy);

        ViewBag.SourceHierarchy = hierarchy;
        return View(viewModel);
    }

    // Task 4.6: Constructor-Based Instantiation (ConstructUsing)
    public IActionResult ConstructUsing()
    {
        var source = MockDatabase.GetSampleProductSource();
        var immutableProduct = _engine.Map<WebProductSource, WebImmutableProduct>(source);

        ViewBag.SourceProduct = source;
        return View(immutableProduct);
    }

    // Task 4.7: Circular References & Recursion Protection (PreserveReferences, MaxDepth)
    public IActionResult CircularReferences()
    {
        var root = MockDatabase.GetSampleCircularEmployeeTree();
        var viewModel = _engine.Map<WebCircularEmployee, WebCircularEmployeeViewModel>(root);

        // Verify that lead's manager refers to the root director instance
        bool isIdentityPreserved = false;
        if (viewModel.Subordinates.Count > 0)
        {
            var lead = viewModel.Subordinates[0];
            isIdentityPreserved = ReferenceEquals(lead.Manager, viewModel);
        }

        ViewBag.IdentityPreserved = isIdentityPreserved;
        return View(viewModel);
    }
}
