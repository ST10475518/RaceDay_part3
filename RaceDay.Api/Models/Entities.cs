using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public enum UserRole { Organiser, Participant }
public enum EventType { Run, Walk, Cycle }
public enum EnrolmentStatus { Pending, Confirmed, Cancelled }

public static class RoleNames
{
    public const string Organiser = nameof(UserRole.Organiser);
    public const string Participant = nameof(UserRole.Participant);
}

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(50)] public string FirstName { get; set; } = "";
    [Required, MaxLength(50)] public string LastName { get; set; } = "";
    [Required, MaxLength(150)] public string Email { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    [MaxLength(20)] public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    /// <summary>URL of the profile picture stored in Azure Blob Storage.</summary>
    [MaxLength(500)] public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RaceEvent> OrganisedEvents { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class RaceEvent
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [Required, MaxLength(1000)] public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    [Required, MaxLength(200)] public string Location { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public EventType EventType { get; set; }
    /// <summary>URL of the event banner stored in Azure Blob Storage.</summary>
    [MaxLength(500)] public string? BannerUrl { get; set; }
    public int OrganiserId { get; set; }
    public User? Organiser { get; set; }

    public List<Category> Categories { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Category
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public RaceEvent? Event { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";   // e.g. "Under 20", "10km"
    [MaxLength(300)] public string? Description { get; set; }
}

public class Enrolment
{
    public int Id { get; set; }
    public int ParticipantId { get; set; }
    public User? Participant { get; set; }
    public int EventId { get; set; }
    public RaceEvent? Event { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Pending;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public Result? Result { get; set; }
}

public class Result
{
    public int Id { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
