using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    [Required, StringLength(50)] public string FirstName { get; set; } = "";
    [Required, StringLength(50)] public string LastName { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
    [Required] public UserRole Role { get; set; }
    [Phone, StringLength(20)] public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    /// <summary>Required only when registering as an Organiser (prevents anyone self-promoting).</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class UpdateProfileRequest
{
    [Required, StringLength(50)] public string FirstName { get; set; } = "";
    [Required, StringLength(50)] public string LastName { get; set; } = "";
    [Phone, StringLength(20)] public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
}

public record UserDto(int Id, string FirstName, string LastName, string Email, UserRole Role,
                      string? PhoneNumber, DateTime? DateOfBirth, string? ProfilePictureUrl);

public class EventRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required] public DateTime Date { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = "";
    [Range(0.1, 1000)] public decimal DistanceKm { get; set; }
    [Required] public EventType EventType { get; set; }
}

public record EventDto(int Id, string Name, string Description, DateTime Date, string Location,
                       decimal DistanceKm, EventType EventType, int OrganiserId, string? BannerUrl);

/// <summary>An event plus counts, used by the Organiser dashboard.</summary>
public record EventSummaryDto(EventDto Event, int EnrolmentCount, int CategoryCount);

public class CategoryRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(300)] public string? Description { get; set; }
}

public record CategoryDto(int Id, int EventId, string Name, string? Description);

public class EnrolRequest
{
    [Range(1, int.MaxValue)] public int EventId { get; set; }
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
}

public class UpdateEnrolmentStatusRequest
{
    [Required] public EnrolmentStatus Status { get; set; }
}

public record EnrolmentDto(int Id, int EventId, string EventName, DateTime EventDate, int CategoryId,
                           string CategoryName, int ParticipantId, string ParticipantName,
                           EnrolmentStatus Status, DateTime EnrolledAt);

public class ResultRequest
{
    [Range(1, int.MaxValue)] public int EnrolmentId { get; set; }
    [Required] public TimeSpan FinishTime { get; set; }
    [Range(1, 100000)] public int Position { get; set; }
}

public class UpdateResultRequest
{
    [Required] public TimeSpan FinishTime { get; set; }
    [Range(1, 100000)] public int Position { get; set; }
}

/// <summary>TotalFinishers = number of results recorded for the event (e.g. "47th of 312").</summary>
public record ResultDto(int Id, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
                        int ParticipantId, string ParticipantName, string CategoryName,
                        TimeSpan FinishTime, int Position, int TotalFinishers, DateTime RecordedAt);

public static class Mapping
{
    public static UserDto ToDto(this User u) =>
        new(u.Id, u.FirstName, u.LastName, u.Email, u.Role, u.PhoneNumber, u.DateOfBirth, u.ProfilePictureUrl);

    public static EventDto ToDto(this RaceEvent e) =>
        new(e.Id, e.Name, e.Description, e.Date, e.Location, e.DistanceKm, e.EventType, e.OrganiserId, e.BannerUrl);

    public static CategoryDto ToDto(this Category c) => new(c.Id, c.EventId, c.Name, c.Description);

    public static EnrolmentDto ToDto(this Enrolment e) =>
        new(e.Id, e.EventId, e.Event!.Name, e.Event.Date, e.CategoryId, e.Category!.Name, e.ParticipantId,
            $"{e.Participant!.FirstName} {e.Participant.LastName}", e.Status, e.EnrolledAt);

    public static ResultDto ToDto(this Result r, int totalFinishers) =>
        new(r.Id, r.EnrolmentId, r.Enrolment!.EventId, r.Enrolment.Event!.Name, r.Enrolment.Event.Date,
            r.Enrolment.ParticipantId,
            $"{r.Enrolment.Participant!.FirstName} {r.Enrolment.Participant.LastName}",
            r.Enrolment.Category!.Name, r.FinishTime, r.Position, totalFinishers, r.RecordedAt);
}
