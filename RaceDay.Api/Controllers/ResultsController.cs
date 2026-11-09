using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Race results: Organisers capture them; Participants view their own.</summary>
[ApiController]
[Authorize]
public class ResultsController(RaceDayDbContext db) : ControllerBase
{
    private IQueryable<Result> Query() => db.Results
        .Include(r => r.Enrolment!).ThenInclude(e => e.Event)
        .Include(r => r.Enrolment!).ThenInclude(e => e.Category)
        .Include(r => r.Enrolment!).ThenInclude(e => e.Participant);

    /// <summary>Number of results recorded per event id (the "out of N finishers" figure).</summary>
    private async Task<Dictionary<int, int>> Totals(IEnumerable<int> eventIds)
    {
        var ids = eventIds.Distinct().ToList();
        return await db.Results.Where(r => ids.Contains(r.Enrolment!.EventId))
            .GroupBy(r => r.Enrolment!.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);
    }

    private static ResultDto Map(Result r, Dictionary<int, int> totals) =>
        r.ToDto(totals.GetValueOrDefault(r.Enrolment!.EventId));

    /// <summary>Record a participant's finish time and position (Organiser who owns the event).</summary>
    /// <remarks>Body: enrolmentId, finishTime (hh:mm:ss), position. One result per enrolment.</remarks>
    /// <response code="201">Result recorded.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Enrolment not found.</response>
    /// <response code="409">A result already exists for this enrolment.</response>
    [HttpPost("api/results")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> Create(ResultRequest req)
    {
        var enrolment = await db.Enrolments.Include(e => e.Event).FirstOrDefaultAsync(e => e.Id == req.EnrolmentId);
        if (enrolment is null) return NotFound(new { message = "Enrolment not found." });
        if (enrolment.Event!.OrganiserId != User.GetUserId()) return Forbid();
        if (await db.Results.AnyAsync(r => r.EnrolmentId == req.EnrolmentId))
            return Conflict(new { message = "A result already exists for this enrolment." });

        var r = new Result { EnrolmentId = req.EnrolmentId, FinishTime = req.FinishTime, Position = req.Position };
        db.Results.Add(r);
        await db.SaveChangesAsync();
        var saved = await Query().AsNoTracking().FirstAsync(x => x.Id == r.Id);
        var totals = await Totals(new[] { saved.Enrolment!.EventId });
        return CreatedAtAction(nameof(Mine), null, Map(saved, totals));
    }

    /// <summary>Correct a result (Organiser who owns the event).</summary>
    /// <response code="200">Updated result.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Result not found.</response>
    [HttpPut("api/results/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> Update(int id, UpdateResultRequest req)
    {
        var r = await Query().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        if (r.Enrolment!.Event!.OrganiserId != User.GetUserId()) return Forbid();
        r.FinishTime = req.FinishTime; r.Position = req.Position;
        await db.SaveChangesAsync();
        var totals = await Totals(new[] { r.Enrolment.EventId });
        return Ok(Map(r, totals));
    }

    /// <summary>List all results for an event (Organiser who owns the event), ordered by position.</summary>
    /// <response code="200">Results for the event.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ResultDto>>> ForEvent(int eventId)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();
        var list = await Query().AsNoTracking().Where(r => r.Enrolment!.EventId == eventId)
                                .OrderBy(r => r.Position).ToListAsync();
        var totals = await Totals(new[] { eventId });
        return Ok(list.Select(r => Map(r, totals)).ToList());
    }

    /// <summary>List the current Participant's own results, with position and total finishers per event.</summary>
    /// <response code="200">The participant's results.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("api/results/my")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ResultDto>>> Mine()
    {
        var me = User.GetUserId();
        var list = await Query().AsNoTracking().Where(r => r.Enrolment!.ParticipantId == me)
                                .OrderByDescending(r => r.Enrolment!.Event!.Date).ToListAsync();
        var totals = await Totals(list.Select(r => r.Enrolment!.EventId));
        return Ok(list.Select(r => Map(r, totals)).ToList());
    }
}
