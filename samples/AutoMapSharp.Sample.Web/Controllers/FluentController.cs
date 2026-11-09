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

    public IActionResult Overview()
    {
        return View();
    }

    public IActionResult SkipAndConstants()
    {
        var entity = MockDatabase.GetSampleUserProfileEntity();
        var viewModel = _engine.Map<UserProfileEntity, UserProfileViewModel>(entity);

        ViewBag.SourceEntity = entity;
        return View(viewModel);
    }

    public IActionResult MapSource()
    {
        var entity = MockDatabase.GetSampleUserProfileEntity();
        var viewModel = _engine.Map<UserProfileEntity, UserProfileViewModel>(entity);

        ViewBag.SourceEntity = entity;
        return View(viewModel);
    }

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

    public IActionResult ValueTransformers()
    {
        var dirtyEntity = MockDatabase.GetSampleDirtyTextEntity();
        var cleanViewModel = _engine.Map<DirtyTextEntity, CleanTextViewModel>(dirtyEntity);

        ViewBag.DirtyEntity = dirtyEntity;
        return View(cleanViewModel);
    }
}
