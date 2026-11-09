using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public record ErrorViewModel(string Title, string Message);

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "First name is required."), StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Last name is required."), StringLength(50)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required."), DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Required(ErrorMessage = "Please choose a role.")]
    public string Role { get; set; } = "Participant";

    [Phone(ErrorMessage = "Enter a valid phone number."), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Organiser code")]
    public string? OrganiserCode { get; set; }
}

public class EventFormViewModel
{
    public int Id { get; set; }
    public bool IsEdit { get; set; }

    [Required(ErrorMessage = "Event name is required."), StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Please describe the event."), StringLength(1000)]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Date and time are required."), DataType(DataType.DateTime)]
    public DateTime Date { get; set; } = DateTime.Now.AddDays(14);

    [Required(ErrorMessage = "Location is required."), StringLength(200)]
    public string Location { get; set; } = "";

    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; } = 10;

    [Required, Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public string? ExistingBannerUrl { get; set; }

    public object ToRequest() => new { name = Name, description = Description, date = Date, location = Location,
                                       distanceKm = DistanceKm, eventType = EventType };
}

public class DashboardViewModel
{
    public List<EventSummaryModel> Events { get; set; } = new();
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Category name is required."), StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(300)]
    public string? Description { get; set; }
}

public class CategoriesViewModel
{
    public EventModel Event { get; set; } = null!;
    public List<CategoryModel> Categories { get; set; } = new();
    public CategoryFormViewModel Form { get; set; } = new();
}

public class EnrolmentsViewModel
{
    public EventModel Event { get; set; } = null!;
    public List<EnrolmentModel> Enrolments { get; set; } = new();
}

public record ResultRow(EnrolmentModel Enrolment, ResultModel? Result);

public class ResultsViewModel
{
    public EventModel Event { get; set; } = null!;
    public List<ResultRow> Rows { get; set; } = new();
}

public class ParticipantHomeViewModel
{
    public List<EventModel> Events { get; set; } = new();
    public HashSet<int> EnrolledEventIds { get; set; } = new();
    public string? Search { get; set; }
    public string? Type { get; set; }
    public string When { get; set; } = "upcoming";
}

public class EventDetailsViewModel
{
    public EventModel Event { get; set; } = null!;
    public List<CategoryModel> Categories { get; set; } = new();
    public EnrolmentModel? MyEnrolment { get; set; }
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "First name is required."), StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Last name is required."), StringLength(50)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    public string? Email { get; set; }
    public string? Role { get; set; }

    [Phone(ErrorMessage = "Enter a valid phone number."), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }

    public string? ProfilePictureUrl { get; set; }

    [Display(Name = "Profile picture")]
    public IFormFile? Picture { get; set; }
}
