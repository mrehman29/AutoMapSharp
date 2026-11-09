using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMapSharp.Sample.Web.Controllers;

public class ProjectionsController : Controller
{
    private readonly SampleDbContext _dbContext;
    private readonly IMapEngine _engine;

    public ProjectionsController(SampleDbContext dbContext, IMapEngine engine)
    {
        _dbContext = dbContext;
        _engine = engine;
    }

    // Task 6.1: Basic Database Projection (ProjectOnto)
    public IActionResult Index()
    {
        var query = _dbContext.Orders.ProjectOnto<OrderProjectionDto>(_engine);
        var orders = query.ToList();

        ViewBag.GeneratedSql = query.ToQueryString();
        return View(orders);
    }

    // Task 6.2: SQL Query Inspector (In-Memory vs ProjectOnto)
    public IActionResult SqlInspector()
    {
        // 1. Heavy in-memory query
        var naiveQuery = _dbContext.Orders
            .Include(o => o.Customer)
                .ThenInclude(c => c!.Address)
            .Include(o => o.Items)
            .Include(o => o.AuditLogs);

        var naiveSql = naiveQuery.ToQueryString();

        // 2. Optimized ProjectOnto projection query
        var projectedQuery = _dbContext.Orders
            .ProjectOnto<OrderProjectionDto>(_engine);

        var projectedSql = projectedQuery.ToQueryString();

        ViewBag.NaiveSql = naiveSql;
        ViewBag.ProjectedSql = projectedSql;

        return View();
    }

    // Task 6.3: Parameterized Database Projections
    public IActionResult Filtered(decimal? minTotal)
    {
        decimal threshold = minTotal ?? 1500m;
        var query = _dbContext.Orders
            .Where(o => o.TotalAmount >= threshold)
            .ProjectOnto<OrderProjectionDto>(_engine, new { minTotal = threshold });

        var results = query.ToList();

        ViewBag.MinTotal = threshold;
        ViewBag.GeneratedSql = query.ToQueryString();
        return View(results);
    }

    // Remediation findings F03 and F04: safe parameterized projections
    public IActionResult SafeParameters(int minQuantity = 2)
    {
        if (minQuantity < 1) minQuantity = 1;
        int comparison = minQuantity == 1 ? 2 : 1;

        var selectedQuery = _dbContext.Orders.OrderBy(o => o.Id)
            .ProjectOnto<BulkOrderSummaryDto>(_engine, new { minQuantity });
        var comparisonQuery = _dbContext.Orders.OrderBy(o => o.Id)
            .ProjectOnto<BulkOrderSummaryDto>(_engine, new { minQuantity = comparison });

        var model = new SafeParametersViewModel
        {
            SelectedMinQuantity = minQuantity,
            ComparisonMinQuantity = comparison,
            SelectedResults = selectedQuery.ToList(),
            ComparisonResults = comparisonQuery.ToList(),
            GeneratedSql = selectedQuery.ToQueryString(),
            ModuleSnippet = AutoMapSharp.Sample.Web.Infrastructure.SnippetReader.Read("ReliabilityWebModules.cs", "SafeParameters"),
            QuerySnippet = "var query = dbContext.Orders\n    .ProjectOnto<BulkOrderSummaryDto>(engine, new { minQuantity });"
        };

        return View(model);
    }

    // Task 6.4: Explicit Expansion for Navigation Properties
    public IActionResult ExplicitExpansion(bool expandItems = false, bool expandAudit = false)
    {
        var membersToExpand = new List<string>();
        if (expandItems) membersToExpand.Add(nameof(OrderProjectionDto.Items));
        if (expandAudit) membersToExpand.Add(nameof(OrderProjectionDto.AuditLogs));

        var query = _dbContext.Orders
            .ProjectOnto<OrderProjectionDto>(_engine, null, membersToExpand.ToArray());

        var results = query.ToList();

        ViewBag.ExpandItems = expandItems;
        ViewBag.ExpandAudit = expandAudit;
        ViewBag.GeneratedSql = query.ToQueryString();

        return View(results);
    }
}
