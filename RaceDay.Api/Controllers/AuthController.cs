using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(RaceDayDbContext db, IConfiguration config) : ControllerBase
{
    private readonly PasswordHasher<User> _hasher = new();

    /// <summary>Register a new Organiser or Participant account.</summary>
    /// <remarks>
    /// Body: first/last name, email, password (min 8 chars), role, optional phone and date of birth.
    /// Registering as an Organiser also requires the correct <c>organiserCode</c>.
    /// The password is stored only as a salted hash.
    /// </remarks>
    /// <response code="201">Account created; returns the user profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Organiser code missing or incorrect.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest req)
    {
        if (req.Role == UserRole.Organiser && req.OrganiserCode != config["Registration:OrganiserCode"])
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Invalid organiser code." });

        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email is already registered." });

        var user = new User
        {
            FirstName = req.FirstName.Trim(), LastName = req.LastName.Trim(), Email = email,
            Role = req.Role, PhoneNumber = req.PhoneNumber, DateOfBirth = req.DateOfBirth
        };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(UsersController.GetMe), "Users", null, user.ToDto());
    }

    /// <summary>Log in with email and password.</summary>
    /// <remarks>On success a session cookie is issued holding the user's id and role.</remarks>
    /// <response code="200">Logged in; returns the user profile.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Ok(user.ToDto());
    }

    /// <summary>End the current session.</summary>
    /// <response code="204">Logged out.</response>
    /// <response code="401">Not logged in.</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
