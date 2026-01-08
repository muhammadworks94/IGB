namespace IGB.Web.ViewModels.People;

public sealed record PersonHeaderVm(
    long UserId,
    string Role,
    string FullName,
    string Email,
    bool IsActive,
    string ApprovalStatus,
    string? Phone,
    string? WhatsApp,
    string? TimeZoneId,
    string? ProfileImagePath
);

public sealed record TutorProfileVm(
    PersonHeaderVm Header,
    List<CourseItemVm> AssignedCourses,
    RatingSummaryVm Rating,
    List<ReviewItemVm> RecentReviews,
    List<StudentMiniVm> Students,
    LessonsSummaryVm Lessons,
    List<TutorClassVm> CurrentClasses,
    List<CourseAssignOptionVm> AssignableCourses
);

public sealed record StudentProfileVm(
    PersonHeaderVm Header,
    StudentAcademicVm Academic,
    List<CourseEnrollmentVm> EnrolledCourses,
    List<StudentLessonVm> UpcomingLessons,
    List<StudentLessonVm> RecentLessons,
    List<GuardianMiniVm> Guardians,
    LessonsSummaryVm Lessons,
    StudentFeedbackSummaryVm Feedback,
    List<StudentFeedbackItemVm> RecentFeedback,
    List<StudentProgressNoteVm> RecentProgressNotes,
    List<StudentTestVm> RecentTests
);

public sealed record GuardianProfileVm(
    PersonHeaderVm Header,
    List<WardVm> Wards
);

public sealed record UserProfileVm(
    PersonHeaderVm Header,
    DateTime CreatedAtUtc,
    List<UserRbacRoleVm> AssignedRbacRoles
);

public sealed record UserRbacRoleVm(long RoleId, string Name, bool IsSystem);

public sealed record CourseItemVm(long CourseId, string CourseName, string? Grade, string? Curriculum, bool IsActive);
public sealed record CourseEnrollmentVm(long CourseId, string CourseName, string? TutorName, string Status);
public sealed record CourseAssignOptionVm(long CourseId, string CourseName, string? Grade, string? Curriculum);

public sealed record RatingSummaryVm(double Average, int Reviews, List<int> Stars, List<int> Counts);
public sealed record ReviewItemVm(
    long LessonId,
    string WhenUtc,
    string CourseName,
    int Rating,
    string StudentDisplayName,
    string? Comment
);

public sealed record StudentMiniVm(long StudentId, string Name);
public sealed record GuardianMiniVm(long GuardianUserId, string Name, string? Email, string? Phone, string? WhatsApp);
public sealed record WardVm(long StudentId, string StudentName, List<CourseEnrollmentVm> Courses);

public sealed record LessonsSummaryVm(int Total, int Upcoming, int Completed, int Cancelled);

public sealed record StudentAcademicVm(string? Curriculum, string? Grade);

public sealed record StudentLessonVm(
    long LessonId,
    string CourseName,
    string TutorName,
    string Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool CanJoin,
    string? JoinUrl
);

public sealed record StudentFeedbackSummaryVm(double Average, int Count);

public sealed record StudentFeedbackItemVm(
    long LessonId,
    DateTime CreatedAtUtc,
    string CourseName,
    string TutorName,
    int Rating,
    string? Comments
);

public sealed record StudentProgressNoteVm(
    DateTime CreatedAtUtc,
    string CourseName,
    string TutorName,
    string Note
);

public sealed record StudentTestVm(
    long TestReportId,
    DateOnly TestDate,
    string TestName,
    string CourseName,
    string TutorName,
    decimal Percentage,
    string Grade
);

public sealed record TutorClassVm(
    long LessonId,
    long StudentId,
    string CourseName,
    string StudentName,
    string Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc
);


