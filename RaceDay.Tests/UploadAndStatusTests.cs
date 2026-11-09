using System.Net;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Part 3 additions: image uploads (Blob Storage via the API), enrolment status, result totals.</summary>
public class UploadAndStatusTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Organiser_CanUploadEventBanner_AndUrlIsSaved()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(org);

        var res = await org.PostAsync($"/api/events/{ev.Id}/banner", Api.FileContent("banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await res.Read<EventDto>();
        Assert.Contains("/banners/", updated!.BannerUrl);

        // Persisted: fetching the event again returns the URL
        var fetched = await (await org.GetAsync($"/api/events/{ev.Id}")).Read<EventDto>();
        Assert.Equal(updated.BannerUrl, fetched!.BannerUrl);
    }

    [Fact]
    public async Task Participant_CannotUploadEventBanner()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org);
        var res = await part.PostAsync($"/api/events/{ev.Id}/banner", Api.FileContent("banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadBannerForAnotherOrganisersEvent()
    {
        var (owner, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (other, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(owner);
        var res = await other.PostAsync($"/api/events/{ev.Id}/banner", Api.FileContent("banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_RejectsNonImageFiles()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(org);
        var res = await org.PostAsync($"/api/events/{ev.Id}/banner", Api.FileContent("notes.txt", "text/plain"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_Unauthenticated_Returns401()
    {
        var res = await factory.CreateClient().PostAsync("/api/events/1/banner", Api.FileContent("b.png", "image/png"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task User_CanUploadProfilePicture_AndUrlIsSaved()
    {
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var res = await part.PostAsync("/api/users/me/picture", Api.FileContent("me.jpg", "image/jpeg"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("/profiles/", (await res.Read<UserDto>())!.ProfilePictureUrl);

        var me = await (await part.GetAsync("/api/users/me")).Read<UserDto>();
        Assert.NotNull(me!.ProfilePictureUrl);
    }

    [Fact]
    public async Task NewEnrolment_StartsPending_AndOrganiserCanConfirm()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev.Id);
        var enrolment = await (await part.Post("/api/enrolments",
            new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id })).Read<EnrolmentDto>();
        Assert.Equal(EnrolmentStatus.Pending, enrolment!.Status);

        var res = await org.Put($"/api/enrolments/{enrolment.Id}/status",
            new UpdateEnrolmentStatusRequest { Status = EnrolmentStatus.Confirmed });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(EnrolmentStatus.Confirmed, (await res.Read<EnrolmentDto>())!.Status);

        var mine = await (await part.GetAsync("/api/enrolments/my")).Read<List<EnrolmentDto>>();
        Assert.Equal(EnrolmentStatus.Confirmed, mine!.Single(e => e.Id == enrolment.Id).Status);
    }

    [Fact]
    public async Task Participant_CannotChangeEnrolmentStatus()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev.Id);
        var enrolment = await (await part.Post("/api/enrolments",
            new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id })).Read<EnrolmentDto>();
        var res = await part.Put($"/api/enrolments/{enrolment!.Id}/status",
            new UpdateEnrolmentStatusRequest { Status = EnrolmentStatus.Confirmed });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_MineEndpoint_ReturnsOnlyOwnEventsWithEnrolmentCounts()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (other, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var (part, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ev = await Api.CreateEvent(org);
        await Api.CreateEvent(other, "Someone else's event");
        var cat = await Api.CreateCategory(org, ev.Id);
        await part.Post("/api/enrolments", new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id });

        var mine = await (await org.GetAsync("/api/events/mine")).Read<List<EventSummaryDto>>();
        var summary = Assert.Single(mine!);
        Assert.Equal(ev.Id, summary.Event.Id);
        Assert.Equal(1, summary.EnrolmentCount);
        Assert.Equal(1, summary.CategoryCount);

        Assert.Equal(HttpStatusCode.Forbidden, (await part.GetAsync("/api/events/mine")).StatusCode);
    }

    [Fact]
    public async Task MyResults_IncludesTotalFinishersForTheEvent()
    {
        var (org, _) = await Api.LoginAs(factory, UserRole.Organiser);
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev.Id);
        var (a, _) = await Api.LoginAs(factory, UserRole.Participant);
        var (b, _) = await Api.LoginAs(factory, UserRole.Participant);
        var ea = await (await a.Post("/api/enrolments", new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id })).Read<EnrolmentDto>();
        var eb = await (await b.Post("/api/enrolments", new EnrolRequest { EventId = ev.Id, CategoryId = cat.Id })).Read<EnrolmentDto>();
        await org.Post("/api/results", new ResultRequest { EnrolmentId = ea!.Id, FinishTime = TimeSpan.FromMinutes(40), Position = 1 });
        await org.Post("/api/results", new ResultRequest { EnrolmentId = eb!.Id, FinishTime = TimeSpan.FromMinutes(45), Position = 2 });

        var mine = await (await b.GetAsync("/api/results/my")).Read<List<ResultDto>>();
        var result = Assert.Single(mine!);
        Assert.Equal(2, result.Position);
        Assert.Equal(2, result.TotalFinishers);
    }
}
