using System.Net;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController(ApiClient api) : AppController
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (CurrentRole is not null) return HomeFor(CurrentRole);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        HttpContext.Session.Clear();   // never reuse a stale session
        var result = await api.LoginAsync<UserModel>(model.Email, model.Password);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty,
                result.Status == HttpStatusCode.Unauthorized ? "Invalid email or password."
                                                              : result.Message ?? "Login failed. Please try again.");
            return View(model);
        }

        var user = result.Data!;
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.Id);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);
        HttpContext.Session.SetString(SessionKeys.Name, $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString(SessionKeys.Picture, user.ProfilePictureUrl ?? "");

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return HomeFor(user.Role);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (CurrentRole is not null) return HomeFor(CurrentRole);
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role == Roles.Organiser && string.IsNullOrWhiteSpace(model.OrganiserCode))
            ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need the organiser code.");
        if (!ModelState.IsValid) return View(model);

        var result = await api.PostAsync<UserModel>("api/auth/register", new
        {
            firstName = model.FirstName,
            lastName = model.LastName,
            email = model.Email,
            password = model.Password,
            role = model.Role,
            phoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber,
            dateOfBirth = model.DateOfBirth,
            organiserCode = model.OrganiserCode
        });

        if (!result.Success)
        {
            if (result.Status == HttpStatusCode.Conflict)
                ModelState.AddModelError(nameof(model.Email), "That email is already registered.");
            else if (result.Status == HttpStatusCode.Forbidden)
                ModelState.AddModelError(nameof(model.OrganiserCode), "That organiser code is not valid.");
            else
                AddApiErrors(result, typeof(RegisterViewModel));
            return View(model);
        }

        TempData["Success"] = "Your account has been created. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try { await api.PostAsync("api/auth/logout", null); }
        catch (ApiUnauthorizedException) { /* already expired - nothing to end on the API */ }
        catch (ApiUnavailableException) { /* API down - we still clear our own session below */ }

        HttpContext.Session.Clear();   // clears user id, role and the API cookie
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
