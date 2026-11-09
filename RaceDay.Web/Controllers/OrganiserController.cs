using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Roles.Organiser)]
public class OrganiserController(ApiClient api) : AppController
{
    // ---------------------------------------------------------------- Dashboard
    public async Task<IActionResult> Dashboard()
    {
        var result = await api.GetAsync<List<EventSummaryModel>>("api/events/mine");
        if (!result.Success) return ShowError(result);
        return View(new DashboardViewModel { Events = result.Data ?? new() });
    }

    // ---------------------------------------------------------------- Events
    public async Task<IActionResult> Events()
    {
        var result = await api.GetAsync<List<EventSummaryModel>>("api/events/mine");
        if (!result.Success) return ShowError(result);
        return View(result.Data ?? new List<EventSummaryModel>());
    }

    [HttpGet]
    public IActionResult CreateEvent() => View("EventForm", new EventFormViewModel
    {
        Date = DateTime.Today.AddDays(14).AddHours(8)
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await api.PostAsync<EventModel>("api/events", model.ToRequest());
        if (!result.Success)
        {
            AddApiErrors(result, typeof(EventFormViewModel));
            return View("EventForm", model);
        }

        var warning = await UploadBannerAsync(result.Data!.Id, model.Banner);
        TempData["Success"] = $"Event \"{result.Data.Name}\" was created. Add categories so participants can enter.";
        if (warning is not null) TempData["Warning"] = warning;
        return RedirectToAction(nameof(Categories), new { eventId = result.Data.Id });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var (ev, error) = await LoadOwnedEventAsync(id);
        if (error is not null) return error;
        return View("EventForm", new EventFormViewModel
        {
            Id = ev!.Id, IsEdit = true, Name = ev.Name, Description = ev.Description, Date = ev.Date,
            Location = ev.Location, DistanceKm = ev.DistanceKm, EventType = ev.EventType,
            ExistingBannerUrl = api.ResolveImage(ev.BannerUrl)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.Id = id; model.IsEdit = true;
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await api.PutAsync<EventModel>($"api/events/{id}", model.ToRequest());
        if (!result.Success)
        {
            AddApiErrors(result, typeof(EventFormViewModel));
            model.ExistingBannerUrl = api.ResolveImage((await api.GetAsync<EventModel>($"api/events/{id}")).Data?.BannerUrl);
            return View("EventForm", model);
        }

        var warning = await UploadBannerAsync(id, model.Banner);
        TempData["Success"] = "Event updated.";
        if (warning is not null) TempData["Warning"] = warning;
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await api.DeleteAsync($"api/events/{id}");
        if (result.Success) TempData["Success"] = "Event deleted.";
        else TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Events));
    }

    // ---------------------------------------------------------------- Categories
    [HttpGet]
    public async Task<IActionResult> Categories(int eventId)
    {
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (error is not null) return error;
        var categories = await api.GetAsync<List<CategoryModel>>($"api/events/{eventId}/categories");
        if (!categories.Success) return ShowError(categories);
        return View(new CategoriesViewModel
        {
            Event = ev!, Categories = categories.Data ?? new(), Form = new CategoryFormViewModel { EventId = eventId }
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(CategoryFormViewModel form)
    {
        var (ev, error) = await LoadOwnedEventAsync(form.EventId);
        if (error is not null) return error;

        if (ModelState.IsValid)
        {
            var result = await api.PostAsync<CategoryModel>($"api/events/{form.EventId}/categories",
                new { name = form.Name, description = form.Description });
            if (result.Success)
            {
                TempData["Success"] = $"Category \"{form.Name}\" added.";
                return RedirectToAction(nameof(Categories), new { eventId = form.EventId });
            }
            AddApiErrors(result, typeof(CategoryFormViewModel));
        }

        var categories = await api.GetAsync<List<CategoryModel>>($"api/events/{form.EventId}/categories");
        return View("Categories", new CategoriesViewModel { Event = ev!, Categories = categories.Data ?? new(), Form = form });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int categoryId, int eventId)
    {
        var result = await api.DeleteAsync($"api/categories/{categoryId}");
        if (result.Success) TempData["Success"] = "Category removed.";
        else TempData["Error"] = result.Status == System.Net.HttpStatusCode.Conflict
            ? "That category already has enrolments, so it can't be removed."
            : result.Message;
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // ---------------------------------------------------------------- Enrolments
    [HttpGet]
    public async Task<IActionResult> Enrolments(int eventId)
    {
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (error is not null) return error;
        var enrolments = await api.GetAsync<List<EnrolmentModel>>($"api/events/{eventId}/enrolments");
        if (!enrolments.Success) return ShowError(enrolments);
        return View(new EnrolmentsViewModel { Event = ev!, Enrolments = enrolments.Data ?? new() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int enrolmentId, int eventId, string status)
    {
        var result = await api.PutAsync<EnrolmentModel>($"api/enrolments/{enrolmentId}/status", new { status });
        if (result.Success) TempData["Success"] = $"Enrolment marked {status}.";
        else TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Enrolments), new { eventId });
    }

    // ---------------------------------------------------------------- Results
    [HttpGet]
    public async Task<IActionResult> Results(int eventId)
    {
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (error is not null) return error;

        var enrolments = await api.GetAsync<List<EnrolmentModel>>($"api/events/{eventId}/enrolments");
        if (!enrolments.Success) return ShowError(enrolments);
        var results = await api.GetAsync<List<ResultModel>>($"api/events/{eventId}/results");
        if (!results.Success) return ShowError(results);

        var rows = (enrolments.Data ?? new()).Where(e => e.Status != "Cancelled")
            .Select(e => new ResultRow(e, results.Data?.FirstOrDefault(r => r.EnrolmentId == e.Id)))
            .OrderBy(r => r.Result?.Position ?? int.MaxValue).ThenBy(r => r.Enrolment.ParticipantName)
            .ToList();
        return View(new ResultsViewModel { Event = ev!, Rows = rows });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResult(int eventId, int enrolmentId, int? resultId, string? finishTime, int position)
    {
        var formats = new[] { @"h\:mm\:ss", @"hh\:mm\:ss" };
        if (!TimeSpan.TryParseExact(finishTime?.Trim(), formats, CultureInfo.InvariantCulture, out var time)
            || time <= TimeSpan.Zero)
        {
            TempData["Error"] = "Enter the finish time as hh:mm:ss (for example 00:52:30).";
            return RedirectToAction(nameof(Results), new { eventId });
        }
        if (position < 1)
        {
            TempData["Error"] = "Finishing position must be 1 or higher.";
            return RedirectToAction(nameof(Results), new { eventId });
        }

        ApiResult result = resultId is > 0
            ? await api.PutAsync<ResultModel>($"api/results/{resultId}", new { finishTime = time, position })
            : await api.PostAsync<ResultModel>("api/results", new { enrolmentId, finishTime = time, position });

        if (result.Success) TempData["Success"] = "Result saved.";
        else TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Results), new { eventId });
    }

    // ---------------------------------------------------------------- helpers
    private async Task<(EventModel? Event, IActionResult? Error)> LoadOwnedEventAsync(int eventId)
    {
        var result = await api.GetAsync<EventModel>($"api/events/{eventId}");
        if (!result.Success) return (null, ShowError(result));
        if (result.Data!.OrganiserId != CurrentUserId) return (null, RedirectToAction("AccessDenied", "Account"));
        return (result.Data, null);
    }

    private async Task<string?> UploadBannerAsync(int eventId, IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;
        var upload = await api.PostFileAsync<EventModel>($"api/events/{eventId}/banner", file);
        return upload.Success ? null : $"The event was saved, but the banner was not uploaded: {upload.Message}";
    }
}
