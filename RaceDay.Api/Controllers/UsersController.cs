using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>View and update the logged-in user's own profile (both roles).</summary>
[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(RaceDayDbContext db) : ControllerBase
{
    /// <summary>Get the current user's profile.</summary>
    /// <response code="200">The profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        return user is null ? Unauthorized() : Ok(user.ToDto());
    }

    /// <summary>Update the current user's profile.</summary>
    /// <remarks>Body: firstName, lastName, optional phoneNumber and dateOfBirth. Email and role cannot be changed.</remarks>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user is null) return Unauthorized();
        user.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.PhoneNumber = req.PhoneNumber;
        user.DateOfBirth = req.DateOfBirth;
        await db.SaveChangesAsync();
        return Ok(user.ToDto());
    }

    /// <summary>Upload or replace the current user's profile picture.</summary>
    /// <remarks>Multipart form with a single <c>file</c> field (JPG, PNG, WEBP or GIF, max 5 MB).
    /// The image is stored in Azure Blob Storage and its URL is saved on the user.</remarks>
    /// <response code="200">Updated profile including <c>profilePictureUrl</c>.</response>
    /// <response code="400">Missing, oversized or unsupported file.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("me/picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> UploadPicture(IFormFile file, [FromServices] IBlobStorageService storage)
    {
        var error = ImageRules.Validate(file);
        if (error is not null) return BadRequest(new { message = error });

        var user = await db.Users.FindAsync(User.GetUserId());
        if (user is null) return Unauthorized();

        await using var stream = file.OpenReadStream();
        user.ProfilePictureUrl = await storage.UploadAsync(stream, file.FileName, file.ContentType, "profiles");
        await db.SaveChangesAsync();
        return Ok(user.ToDto());
    }
}
