using System;
using AutoMapSharp;
using AutoMapSharp.Sample.Web.Data;
using AutoMapSharp.Sample.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutoMapSharp.Sample.Web.Controllers;

public class FluentController : Controller
{
    private readonly IMapEngine _engine;

    public FluentController(IMapEngine engine)
    {
        _engine = engine;
    }

    // Task 3.1: Fluent Mapping Definition Overview
    public IActionResult Overview()
    {
        return View();
    }

    // Task 3.2: Property Skipping & Constant Values (Skip, UseValue)
    public IActionResult SkipAndConstants()
    {
        var entity = MockDatabase.GetSampleUserProfileEntity();
        var viewModel = _engine.Map<UserProfileEntity, UserProfileViewModel>(entity);

        ViewBag.SourceEntity = entity;
        return View(viewModel);
    }

    // Task 3.3: Custom Source Property Redirection (MapSource)
    public IActionResult MapSource()
    {
        var entity = MockDatabase.GetSampleUserProfileEntity();
        var viewModel = _engine.Map<UserProfileEntity, UserProfileViewModel>(entity);

        ViewBag.SourceEntity = entity;
        return View(viewModel);
    }

    // Task 3.4: Conditionals & Fallbacks (If, Fallback)
    [HttpGet]
    public IActionResult Conditionals()
    {
        var formModel = new ConditionFormModel
        {
            Age = 28,
            AnnualSpend = 1250m,
            Bio = null // Trigger fallback
        };

        var result = _engine.Map<ConditionFormModel, ConditionResultViewModel>(formModel);
        ViewBag.FormModel = formModel;
        return View(result);
    }

    [HttpPost]
    public IActionResult Conditionals(ConditionFormModel formModel)
    {
        var result = _engine.Map<ConditionFormModel, ConditionResultViewModel>(formModel);
        ViewBag.FormModel = formModel;
        return View(result);
    }

    // Task 3.5: Bidirectional Mapping (ReverseMap)
    public IActionResult ReverseMap(string direction = "forward")
    {
        ViewBag.Direction = direction;

        if (direction == "reverse")
        {
            var dto = new ReverseEmployeeDto
            {
                Id = 901,
                DisplayName = "Margaret Hamilton",
                Department = "Software Engineering",
                ConfidentialCode = null
            };

            var entity = _engine.Map<ReverseEmployeeDto, ReverseEmployeeEntity>(dto);
            ViewBag.SourceDto = dto;
            ViewBag.MappedEntity = entity;
        }
        else
        {
            var entity = MockDatabase.GetSampleReverseEmployeeEntity();
            var dto = _engine.Map<ReverseEmployeeEntity, ReverseEmployeeDto>(entity);
            ViewBag.SourceEntity = entity;
            ViewBag.MappedDto = dto;
        }

        return View();
    }

    // Task 3.6: Global & Type-Level Value Transformers
    public IActionResult ValueTransformers()
    {
        var dirtyEntity = MockDatabase.GetSampleDirtyTextEntity();
        var cleanViewModel = _engine.Map<DirtyTextEntity, CleanTextViewModel>(dirtyEntity);

        ViewBag.DirtyEntity = dirtyEntity;
        return View(cleanViewModel);
    }
}
