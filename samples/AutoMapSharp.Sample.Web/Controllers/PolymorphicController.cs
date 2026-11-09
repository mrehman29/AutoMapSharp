using System;
using System.Collections.Generic;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class PolymorphicController : Controller
{
    private readonly IMapEngine _engine;

    public PolymorphicController(IMapEngine engine)
    {
        _engine = engine;
    }

    public IActionResult Billing()
    {
        var payments = MockDatabase.GetSamplePayments();
        var viewModels = _engine.Map<List<WebPayment>, List<WebPaymentViewModel>>(payments);

        ViewBag.SourcePayments = payments;
        return View(viewModels);
    }
}
