using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared helpers for every controller.</summary>
public abstract class AppController : Controller
{
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    /// <summary>Sends each role to its own landing page.</summary>
    protected IActionResult HomeFor(string? role) => role switch
    {
        Roles.Organiser => RedirectToAction("Dashboard", "Organiser"),
        Roles.Participant => RedirectToAction("Index", "Participant"),
        _ => RedirectToAction("Index", "Home")
    };

    /// <summary>Copies API validation errors onto the matching form fields.</summary>
    protected void AddApiErrors(ApiResult result, Type modelType)
    {
        if (result.Errors.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(result.Message)) ModelState.AddModelError(string.Empty, result.Message);
            return;
        }
        foreach (var (key, messages) in result.Errors)
        {
            var property = modelType.GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            foreach (var message in messages)
                ModelState.AddModelError(property?.Name ?? string.Empty, message);
        }
    }

    protected IActionResult ShowError(ApiResult result)
    {
        if (result.Status == System.Net.HttpStatusCode.Forbidden) return RedirectToAction("AccessDenied", "Account");
        Response.StatusCode = result.Status == System.Net.HttpStatusCode.NotFound ? 404 : 500;
        return View("Error", new ErrorViewModel(
            result.Status == System.Net.HttpStatusCode.NotFound ? "Not found" : "Something went wrong",
            result.Message ?? "Please try again."));
    }
}
