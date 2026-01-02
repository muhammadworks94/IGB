namespace IGB.Web.ViewModels.Feedback;

public sealed record TutorFeedbackDetailsViewModel(
    long LessonId,
    long TutorUserId,
    string TutorName,
    long StudentUserId,
    string StudentDisplayName,
    long CourseId,
    string CourseName,
    DateTime CreatedAtUtc,
    int Rating,
    int SubjectKnowledge,
    int Communication,
    int Punctuality,
    int TeachingMethod,
    int Friendliness,
    string? Comments,
    bool IsAnonymous
);


