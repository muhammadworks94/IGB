using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace IGB.Web.ViewModels;

public class ProfileViewModel
{
    public long Id { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? LocalNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? TimeZoneId { get; set; }

    public string? ProfileImagePath { get; set; }
    public IFormFile? ProfileImage { get; set; }

    // UI helpers
    public int CompletionPercent { get; set; }

    // Student guardians (up to 2)
    public List<GuardianInputViewModel> Guardians { get; set; } = new();

    // Student profile
    public DateTime? StudentDateOfBirth { get; set; }
    public long? StudentCurriculumId { get; set; }
    public long? StudentGradeId { get; set; }
    public List<LookupItem> Curricula { get; set; } = new();
    public List<LookupItem> Grades { get; set; } = new();
    public List<EnrolledCourseItem> EnrolledCourses { get; set; } = new();
    public List<ScheduleItem> Schedule { get; set; } = new();
    public int RemainingCredits { get; set; } // placeholder until ledger is implemented

    // Tutor profile
    public DateTime? TutorDateOfBirth { get; set; }
    public string? TutorSpecialitiesCsv { get; set; }
    public string? TutorEducationJson { get; set; }
    public string? TutorWorkJson { get; set; }
    public List<UserDocumentItem> TutorDocuments { get; set; } = new();
    public decimal Earnings { get; set; } // placeholder until earnings module exists

    // Tutor feedback summary
    public double TutorAverageRating { get; set; } = 0;
    public int TutorReviewCount { get; set; } = 0;
    public List<TutorReviewSnippet> TutorRecentReviews { get; set; } = new();
}

public class GuardianInputViewModel
{
    public long? Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string? Email { get; set; }
    public string? LocalNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public bool IsPrimary { get; set; }
}

public record LookupItem(long Id, string Name);
public record EnrolledCourseItem(long BookingId, string CourseName, string GradeName, string CurriculumName, string Status, DateTime RequestedAt);
public record ScheduleItem(long LessonId, string CourseName, string Status, DateTime WhenUtc, int DurationMinutes);
public record UserDocumentItem(long Id, string Type, string FileName, long SizeBytes, string FilePath, DateTime CreatedAtUtc);
public record TutorReviewSnippet(int Rating, string FromName, string? Comments, DateTime CreatedAtUtc);


