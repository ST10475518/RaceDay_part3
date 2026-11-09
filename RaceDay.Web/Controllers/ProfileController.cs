using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize]   // any logged-in user
public class ProfileController(ApiClient api) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await api.GetAsync<UserModel>("api/users/me");
        if (!result.Success) return ShowError(result);
        return View(ToViewModel(result.Data!));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileViewModel model)
    {
        var current = await api.GetAsync<UserModel>("api/users/me");
        if (!current.Success) return ShowError(current);
        model.Email = current.Data!.Email;
        model.Role = current.Data.Role;
        model.ProfilePictureUrl = api.ResolveImage(current.Data.ProfilePictureUrl);
        if (!ModelState.IsValid) return View(model);

        var update = await api.PutAsync<UserModel>("api/users/me", new
        {
            firstName = model.FirstName,
            lastName = model.LastName,
            phoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber,
            dateOfBirth = model.DateOfBirth
        });
        if (!update.Success)
        {
            AddApiErrors(update, typeof(ProfileViewModel));
            return View(model);
        }

        var user = update.Data!;
        string? warning = null;
        if (model.Picture is { Length: > 0 })
        {
            var upload = await api.PostFileAsync<UserModel>("api/users/me/picture", model.Picture);
            if (upload.Success) user = upload.Data!;
            else warning = $"Your details were saved, but the picture was not uploaded: {upload.Message}";
        }

        HttpContext.Session.SetString(SessionKeys.Name, $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString(SessionKeys.Picture, user.ProfilePictureUrl ?? "");

        if (warning is null) TempData["Success"] = "Profile updated.";
        else TempData["Warning"] = warning;
        return RedirectToAction(nameof(Index));
    }

    private ProfileViewModel ToViewModel(UserModel u) => new()
    {
        FirstName = u.FirstName, LastName = u.LastName, Email = u.Email, Role = u.Role,
        PhoneNumber = u.PhoneNumber, DateOfBirth = u.DateOfBirth,
        ProfilePictureUrl = api.ResolveImage(u.ProfilePictureUrl)
    };
}
