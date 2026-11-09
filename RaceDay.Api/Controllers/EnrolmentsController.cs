using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Event enrolments: Participants enter events; Organisers view and confirm entries for their events.</summary>
[ApiController]
[Authorize]
public class EnrolmentsController(RaceDayDbContext db) : ControllerBase
{
    private IQueryable<Enrolment> Query() => db.Enrolments.AsNoTracking()
        .Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant);

    /// <summary>Enter an event in a chosen category (Participant only).</summary>
    /// <remarks>Body: eventId and categoryId. The category must belong to the event; one enrolment per event.
    /// New enrolments start as <c>Pending</c> until the Organiser confirms them.</remarks>
    /// <response code="201">Enrolment recorded (participant, event, category).</response>
    /// <response code="400">Category does not belong to the event, or the event has passed.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="404">Event not found.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost("api/enrolments")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDto>> Enrol(EnrolRequest req)
    {
        var ev = await db.Events.FindAsync(req.EventId);
        if (ev is null) return NotFound(new { message = "Event not found." });
        if (ev.Date < DateTime.UtcNow) return BadRequest(new { message = "This event has already taken place." });
        if (!await db.Categories.AnyAsync(c => c.Id == req.CategoryId && c.EventId == req.EventId))
            return BadRequest(new { message = "Category does not belong to this event." });

        var me = User.GetUserId();
        if (await db.Enrolments.AnyAsync(e => e.ParticipantId == me && e.EventId == req.EventId))
            return Conflict(new { message = "You are already enrolled in this event." });

        var enrolment = new Enrolment { ParticipantId = me, EventId = req.EventId, CategoryId = req.CategoryId };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();
        var saved = await Query().FirstAsync(e => e.Id == enrolment.Id);
        return CreatedAtAction(nameof(Mine), null, saved.ToDto());
    }

    /// <summary>List the current Participant's enrolments.</summary>
    /// <response code="200">The participant's enrolments.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("api/enrolments/my")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EnrolmentDto>>> Mine()
    {
        var me = User.GetUserId();
        var list = await Query().Where(e => e.ParticipantId == me).OrderBy(e => e.Event!.Date).ToListAsync();
        return Ok(list.Select(e => e.ToDto()).ToList());
    }

    /// <summary>Withdraw from an event (Participant, own enrolment only). Removes any result too.</summary>
    /// <response code="204">Enrolment removed.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="404">Enrolment not found (or belongs to someone else).</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var me = User.GetUserId();
        var e = await db.Enrolments.FirstOrDefaultAsync(x => x.Id == id && x.ParticipantId == me);
        if (e is null) return NotFound();
        db.Enrolments.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>List all enrolments for an event (Organiser who owns the event).</summary>
    /// <response code="200">All enrolments for the event.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EnrolmentDto>>> ForEvent(int eventId)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();
        var list = await Query().Where(e => e.EventId == eventId).OrderBy(e => e.EnrolledAt).ToListAsync();
        return Ok(list.Select(e => e.ToDto()).ToList());
    }

    /// <summary>Set an enrolment's status to Pending, Confirmed or Cancelled (Organiser who owns the event).</summary>
    /// <remarks>Body: <c>{ "status": "Confirmed" }</c></remarks>
    /// <response code="200">Updated enrolment.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Enrolment not found.</response>
    [HttpPut("api/enrolments/{id:int}/status")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentDto>> SetStatus(int id, UpdateEnrolmentStatusRequest req)
    {
        var e = await db.Enrolments.Include(x => x.Event).FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        if (e.Event!.OrganiserId != User.GetUserId()) return Forbid();
        e.Status = req.Status;
        await db.SaveChangesAsync();
        var saved = await Query().FirstAsync(x => x.Id == id);
        return Ok(saved.ToDto());
    }
}
