using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Events. Everyone logged in can view; only Organisers can manage (their own events).</summary>
[ApiController]
[Authorize]
[Route("api/events")]
public class EventsController(RaceDayDbContext db) : ControllerBase
{
    /// <summary>List events (both roles).</summary>
    /// <remarks>Optional query filters: <c>type</c> (Run, Walk, Cycle), <c>upcomingOnly</c> and
    /// <c>search</c> (matches name or location).</remarks>
    /// <response code="200">List of events ordered by date.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<EventDto>>> GetAll([FromQuery] EventType? type,
        [FromQuery] bool upcomingOnly = false, [FromQuery] string? search = null)
    {
        var q = db.Events.AsNoTracking().AsQueryable();
        if (type is not null) q = q.Where(e => e.EventType == type);
        if (upcomingOnly) q = q.Where(e => e.Date >= DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(e => e.Name.Contains(s) || e.Location.Contains(s));
        }
        var list = await q.OrderBy(e => e.Date).ToListAsync();
        return Ok(list.Select(e => e.ToDto()).ToList());
    }

    /// <summary>List the current Organiser's own events with enrolment and category counts (Organiser only).</summary>
    /// <response code="200">The organiser's events, ordered by date.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EventSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EventSummaryDto>>> Mine()
    {
        var me = User.GetUserId();
        var rows = await db.Events.AsNoTracking().Where(e => e.OrganiserId == me).OrderBy(e => e.Date)
            .Select(e => new { Event = e, Enrolments = e.Enrolments.Count, Categories = e.Categories.Count })
            .ToListAsync();
        return Ok(rows.Select(r => new EventSummaryDto(r.Event.ToDto(), r.Enrolments, r.Categories)).ToList());
    }

    /// <summary>Get one event by id (both roles).</summary>
    /// <response code="200">The event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> Get(int id)
    {
        var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return e is null ? NotFound() : Ok(e.ToDto());
    }

    /// <summary>Create an event (Organiser only).</summary>
    /// <remarks>Body: name, description, date (must be in the future), location, distanceKm, eventType (Run, Walk, Cycle).
    /// Upload a banner afterwards with POST /api/events/{id}/banner.</remarks>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpPost]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDto>> Create(EventRequest req)
    {
        if (req.Date <= DateTime.UtcNow)
        {
            ModelState.AddModelError(nameof(req.Date), "Event date must be in the future.");
            return ValidationProblem(ModelState);
        }
        var e = new RaceEvent
        {
            Name = req.Name, Description = req.Description, Date = req.Date, Location = req.Location,
            DistanceKm = req.DistanceKm, EventType = req.EventType, OrganiserId = User.GetUserId()
        };
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = e.Id }, e.ToDto());
    }

    /// <summary>Update an event (Organiser who owns it).</summary>
    /// <response code="200">Updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> Update(int id, EventRequest req)
    {
        var e = await db.Events.FindAsync(id);
        if (e is null) return NotFound();
        if (e.OrganiserId != User.GetUserId()) return Forbid();
        e.Name = req.Name; e.Description = req.Description; e.Date = req.Date;
        e.Location = req.Location; e.DistanceKm = req.DistanceKm; e.EventType = req.EventType;
        await db.SaveChangesAsync();
        return Ok(e.ToDto());
    }

    /// <summary>Upload or replace an event banner image (Organiser who owns the event).</summary>
    /// <remarks>Multipart form with a single <c>file</c> field (JPG, PNG, WEBP or GIF, max 5 MB).
    /// The image is stored in Azure Blob Storage and its URL is saved on the event as <c>bannerUrl</c>.</remarks>
    /// <response code="200">Updated event including <c>bannerUrl</c>.</response>
    /// <response code="400">Missing, oversized or unsupported file.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event not found.</response>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = RoleNames.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UploadBanner(int id, IFormFile file, [FromServices] IBlobStorageService storage)
    {
        var e = await db.Events.FindAsync(id);
        if (e is null) return NotFound();
        if (e.OrganiserId != User.GetUserId()) return Forbid();

        var error = ImageRules.Validate(file);
        if (error is not null) return BadRequest(new { message = error });

        await using var stream = file.OpenReadStream();
        e.BannerUrl = await storage.UploadAsync(stream, file.FileName, file.ContentType, "banners");
        await db.SaveChangesAsync();
        return Ok(e.ToDto());
    }

    /// <summary>Delete an event and its categories, enrolments and results (Organiser who owns it).</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await db.Events.Include(x => x.Enrolments).Include(x => x.Categories)
                               .FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        if (e.OrganiserId != User.GetUserId()) return Forbid();
        db.Enrolments.RemoveRange(e.Enrolments);   // removed first because Category->Enrolment is Restrict
        await db.SaveChangesAsync();
        db.Events.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
