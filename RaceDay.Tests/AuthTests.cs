using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(UserRole.Participant)]
    [InlineData(UserRole.Organiser)]
    public async Task Register_ValidUser_Returns201(UserRole role)
    {
        var res = await factory.CreateClient().Post("/api/auth/register", Api.NewRegistration(role));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var user = await res.Read<UserDto>();
        Assert.Equal(role, user!.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = factory.CreateClient();
        var reg = Api.NewRegistration(UserRole.Participant);
        await client.Post("/api/auth/register", reg);
        var res = await client.Post("/api/auth/register", reg);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmailOrShortPassword_Returns400()
    {
        var reg = Api.NewRegistration(UserRole.Participant, "not-an-email");
        reg.Password = "short";
        var res = await factory.CreateClient().Post("/api/auth/register", reg);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_OrganiserWithWrongCode_Returns403()
    {
        var reg = Api.NewRegistration(UserRole.Organiser);
        reg.OrganiserCode = "WRONG";
        var res = await factory.CreateClient().Post("/api/auth/register", reg);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var reg = Api.NewRegistration(UserRole.Participant);
        await factory.CreateClient().Post("/api/auth/register", reg);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = db.Users.Single(u => u.Email == reg.Email.ToLower());
        Assert.NotEqual(reg.Password, stored.PasswordHash);
        Assert.DoesNotContain(reg.Password, stored.PasswordHash);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_AndSessionAllowsProfileAccess()
    {
        var (client, user) = await Api.LoginAs(factory, UserRole.Participant);
        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(user.Email, (await me.Read<UserDto>())!.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = factory.CreateClient();
        var reg = Api.NewRegistration(UserRole.Participant);
        await client.Post("/api/auth/register", reg);
        var res = await client.Post("/api/auth/login", new LoginRequest { Email = reg.Email, Password = "WrongPassword1" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var res = await factory.CreateClient().Post("/api/auth/login",
            new LoginRequest { Email = "nobody@raceday.test", Password = "Whatever123" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("/api/users/me")]
    [InlineData("/api/events")]
    [InlineData("/api/enrolments/my")]
    [InlineData("/api/results/my")]
    public async Task ProtectedEndpoints_WithoutLogin_Return401(string url)
    {
        var res = await factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var (client, _) = await Api.LoginAs(factory, UserRole.Participant);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesOwnDetails()
    {
        var (client, _) = await Api.LoginAs(factory, UserRole.Participant);
        var res = await client.Put("/api/users/me", new UpdateProfileRequest { FirstName = "Updated", LastName = "Name" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Updated", (await res.Read<UserDto>())!.FirstName);
    }
}
