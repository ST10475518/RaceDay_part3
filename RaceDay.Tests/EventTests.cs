using System.Net;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

public class EventTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var (org, user) = await Api.LoginAs(factory, UserRole.Organiser);
        var res = await org.Post("/api/events", Api.NewEvent());
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var ev = await res.Read<EventDto>();
        Assert.Equal(user.Id, ev!.OrganiserId);
        Assert.Equal(EventType.Run, ev.EventType);
    }

    [Fact]
    public async Task Organiser_CreateEvent_InvalidData_Returns400()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var bad = Api.NewEvent(); bad.Name = ""; bad.DistanceKm = 0;
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Post("/api/events", bad)).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(org);
        var update = Api.NewEvent("Renamed Event"); update.EventType = EventType.Cycle;
        var res = await org.Put($"/api/events/{ev.Id}", update);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await res.Read<EventDto>();
        Assert.Equal("Renamed Event", updated!.Name);
        Assert.Equal(EventType.Cycle, updated.EventType);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(org);
        Assert.Equal(HttpStatusCode.NoContent, (await org.DeleteAsync($"/api/events/{ev.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.GetAsync($"/api/events/{ev.Id}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotModifyAnotherOrganisersEvent()
    {
        var (owner, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (other, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Put($"/api/events/{ev.Id}", Api.NewEvent("Hijack"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/events/{ev.Id}")).StatusCode);
    }

    [Fact]
    public async Task UpdateOrDelete_UnknownEvent_Returns404()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Put("/api/events/999999", Api.NewEvent())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.DeleteAsync("/api/events/999999")).StatusCode);
    }

    [Fact]
    public async Task BothRoles_CanViewEvents()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org, "Viewable Event");
        foreach (var client in new[] { org, part })
        {
            var list = await (await client.GetAsync("/api/events")).Read<List<EventDto>>();
            Assert.Contains(list!, e => e.Id == ev.Id);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/events/{ev.Id}")).StatusCode);
        }
    }

    [Fact]
    public async Task Organiser_CanManageCategories_AndBothRolesCanView()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev.Id, "Under 20");

        var rename = await org.Put($"/api/categories/{cat.Id}", new CategoryRequest { Name = "Under 18" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        var list = await (await part.GetAsync($"/api/events/{ev.Id}/categories")).Read<List<CategoryDto>>();
        Assert.Contains(list!, c => c.Name == "Under 18");

        Assert.Equal(HttpStatusCode.NoContent, (await org.DeleteAsync($"/api/categories/{cat.Id}")).StatusCode);
    }
}
