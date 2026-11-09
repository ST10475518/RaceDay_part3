using System.Net;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Verifies the wrong role is rejected (403) on role-specific functionality, both directions.</summary>
public class RoleAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Participant_CannotCreateUpdateOrDeleteEvents()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org);

        Assert.Equal(HttpStatusCode.Forbidden, (await part.Post("/api/events", Api.NewEvent())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await part.Put($"/api/events/{ev.Id}", Api.NewEvent())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await part.DeleteAsync($"/api/events/{ev.Id}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotManageCategories()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org);
        var res = await part.Post($"/api/events/{ev.Id}/categories", new CategoryRequest { Name = "Sneaky" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolmentsOrResultsAsOrganiser()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org);
        Assert.Equal(HttpStatusCode.Forbidden, (await part.GetAsync($"/api/events/{ev.Id}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await part.GetAsync($"/api/events/{ev.Id}/results")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotRecordResults()
    {
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var res = await part.Post("/api/results",
            new ResultRequest { EnrolmentId = 1, FinishTime = TimeSpan.FromMinutes(50), Position = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrolOrViewParticipantOnlyEndpoints()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await org.Post("/api/enrolments", new EnrolRequest { EventId = 1, CategoryId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.GetAsync("/api/enrolments/my")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.GetAsync("/api/results/my")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewAnotherOrganisersEnrolments()
    {
        var (owner, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (other, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{ev.Id}/enrolments")).StatusCode);
    }
}
