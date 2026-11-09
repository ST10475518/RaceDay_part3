namespace RaceDay.Web.Models;

// Lightweight copies of the API's response shapes. They hold data only: no database access, no business rules.
public record UserModel(int Id, string FirstName, string LastName, string Email, string Role,
                        string? PhoneNumber, DateTime? DateOfBirth, string? ProfilePictureUrl);

public record EventModel(int Id, string Name, string Description, DateTime Date, string Location,
                         decimal DistanceKm, string EventType, int OrganiserId, string? BannerUrl);

public record EventSummaryModel(EventModel Event, int EnrolmentCount, int CategoryCount);

public record CategoryModel(int Id, int EventId, string Name, string? Description);

public record EnrolmentModel(int Id, int EventId, string EventName, DateTime EventDate, int CategoryId,
                             string CategoryName, int ParticipantId, string ParticipantName,
                             string Status, DateTime EnrolledAt);

public record ResultModel(int Id, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
                          int ParticipantId, string ParticipantName, string CategoryName,
                          TimeSpan FinishTime, int Position, int TotalFinishers, DateTime RecordedAt);
