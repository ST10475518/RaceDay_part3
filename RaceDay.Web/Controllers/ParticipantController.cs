using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Roles.Participant)]
public class ParticipantController(ApiClient api) : AppController
{
    // ---------------------------------------------------------------- Home: browse + filter
    public async Task<IActionResult> Index(string? search, string? type, string when = "upcoming")
    {
        var query = new List<string>();
        if (when != "all") query.Add("upcomingOnly=true");
        if (!string.IsNullOrWhiteSpace(type)) query.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        var events = await api.GetAsync<List<EventModel>>(url);
        if (!events.Success) return ShowError(events);
        var mine = await api.GetAsync<List<EnrolmentModel>>("api/enrolments/my");

        return View(new ParticipantHomeViewModel
        {
            Events = events.Data ?? new(),
            EnrolledEventIds = (mine.Data ?? new()).Where(e => e.Status != "Cancelled").Select(e => e.EventId).ToHashSet(),
            Search = search, Type = type, When = when
        });
    }

    // ---------------------------------------------------------------- Event detail + enrol
    public async Task<IActionResult> Details(int id)
    {
        var ev = await api.GetAsync<EventModel>($"api/events/{id}");
        if (!ev.Success) return ShowError(ev);
        var categories = await api.GetAsync<List<CategoryModel>>($"api/events/{id}/categories");
        var mine = await api.GetAsync<List<EnrolmentModel>>("api/enrolments/my");

        return View(new EventDetailsViewModel
        {
            Event = ev.Data!,
            Categories = categories.Data ?? new(),
            MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(int eventId, int? categoryId)
    {
        if (categoryId is null or <= 0)
        {
            TempData["Error"] = "Please choose a category before entering.";
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        var result = await api.PostAsync<EnrolmentModel>("api/enrolments", new { eventId, categoryId });
        if (result.Success)
        {
            TempData["Success"] = "You're in! Your enrolment is recorded and awaiting confirmation.";
            return RedirectToAction(nameof(MyEvents));
        }

        TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = eventId });
    }

    // ---------------------------------------------------------------- My events
    public async Task<IActionResult> MyEvents()
    {
        var result = await api.GetAsync<List<EnrolmentModel>>("api/enrolments/my");
        if (!result.Success) return ShowError(result);
        return View(result.Data ?? new List<EnrolmentModel>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id)
    {
        var result = await api.DeleteAsync($"api/enrolments/{id}");
        if (result.Success) TempData["Success"] = "You have withdrawn from the event.";
        else TempData["Error"] = result.Message;
        return RedirectToAction(nameof(MyEvents));
    }

    // ---------------------------------------------------------------- My results
    public async Task<IActionResult> MyResults()
    {
        var result = await api.GetAsync<List<ResultModel>>("api/results/my");
        if (!result.Success) return ShowError(result);
        return View(result.Data ?? new List<ResultModel>());
    }
}
