using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

public class EnrolmentAndResultTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Org, EventDto Ev, CategoryDto Cat)> SetupEvent()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev.Id);
        return (org, ev, cat);
    }

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var (org, ev, cat) = await SetupEvent();
        var (part, user) = await Api.LoginAs(factory, UserRole.Participant);

        var res = await part.Post("/api/enrolments", new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var enrolment = await res.Read<EnrolmentDto>();

        // Recorded in the database with the right participant / event / category link
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            var row = db.Enrolments.Single(e => e.Id == enrolment!.Id);
            Assert.Equal(user.Id, row.ParticipantId);
            Assert.Equal(ev.Id, row.EventId);
            Assert.Equal(cat.Id, row.CategoryId);
        }

        // Visible to the participant ...
        var mine = await (await part.GetAsync("/api/enrolments/my")).Read<List<EnrolmentDto>>();
        Assert.Contains(mine!, e => e.EventId == ev.Id && e.CategoryId == cat.Id);

        // ... and to the organiser who owns the event
        var forEvent = await (await org.GetAsync($"/api/events/{ev.Id}/enrolments")).Read<List<EnrolmentDto>>();
        Assert.Single(forEvent!);
        Assert.Equal(user.Id, forEvent![0].ParticipantId);
    }

    [Fact]
    public async Task Enrol_Twice_Returns409()
    {
        var (_, ev, cat) = await SetupEvent();
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var body = new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id };
        await part.Post("/api/enrolments", body);
        Assert.Equal(HttpStatusCode.Conflict, (await part.Post("/api/enrolments", body)).StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns400()
    {
        var (_, ev, _) = await SetupEvent();
        var (_, _, otherCat) = await SetupEvent();
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var res = await part.Post("/api/enrolments", new EnrolRequest { EventId = ev.Id, CategoryId = otherCat.Id });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownEvent_Returns404()
    {
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var res = await part.Post("/api/enrolments", new EnrolRequest { EventId = 999999, CategoryId = 1 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_Unauthenticated_Returns401()
    {
        var res = await factory.CreateClient().Post("/api/enrolments", new EnrolRequest { EventId = 1, CategoryId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanRecordResult_AndParticipantCanViewOwnResult()
    {
        var (org, ev, cat) = await SetupEvent();
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var enrolment = await (await part.Post("/api/enrolments",
            new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id })).Read<EnrolmentDto>();

        var res = await org.Post("/api/results",
            new ResultRequest { EnrolmentId = enrolment!.Id, FinishTime = new TimeSpan(0, 52, 30), Position = 3 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var mine = await (await part.GetAsync("/api/results/my")).Read<List<ResultDto>>();
        var result = Assert.Single(mine!);
        Assert.Equal(3, result.Position);
        Assert.Equal(new TimeSpan(0, 52, 30), result.FinishTime);
    }

    [Fact]
    public async Task Organiser_CannotRecordDuplicateResult_Returns409()
    {
        var (org, ev, cat) = await SetupEvent();
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var enrolment = await (await part.Post("/api/enrolments",
            new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id })).Read<EnrolmentDto>();
        var body = new ResultRequest { EnrolmentId = enrolment!.Id, FinishTime = TimeSpan.FromMinutes(40), Position = 1 };
        await org.Post("/api/results", body);
        Assert.Equal(HttpStatusCode.Conflict, (await org.Post("/api/results", body)).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotRecordResultForAnotherOrganisersEvent()
    {
        var (_, ev, cat) = await SetupEvent();
        var (other, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var enrolment = await (await part.Post("/api/enrolments",
            new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id })).Read<EnrolmentDto>();
        var res = await other.Post("/api/results",
            new ResultRequest { EnrolmentId = enrolment!.Id, FinishTime = TimeSpan.FromMinutes(40), Position = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Participant_OnlySeesOwnResults()
    {
        var (org, ev, cat) = await SetupEvent();
        var (a, _) = await Api.LoginAs(factory, UserRole.Participant);
        var (b, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ea = await (await a.Post("/api/enrolments", new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id })).Read<EnrolmentDto>();
        await b.Post("/api/enrolments", new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id });
        await org.Post("/api/results", new ResultRequest { EnrolmentId = ea!.Id, FinishTime = TimeSpan.FromMinutes(45), Position = 1 });

        Assert.Single((await (await a.GetAsync("/api/results/my")).Read<List<ResultDto>>())!);
        Assert.Empty((await (await b.GetAsync("/api/results/my")).Read<List<ResultDto>>())!);
    }
}
