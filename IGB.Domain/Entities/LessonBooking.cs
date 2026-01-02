using IGB.Domain.Common;
using IGB.Domain.Enums;

namespace IGB.Domain.Entities;

public class LessonBooking : BaseEntity
{
    // Link to course booking (optional for now but recommended)
    public long? CourseBookingId { get; set; }
    public CourseBooking? CourseBooking { get; set; }

    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public long? TutorUserId { get; set; }
    public User? TutorUser { get; set; }

    // Scheduling window and slot options (3 options)
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }

    public DateTimeOffset Option1 { get; set; }
    public DateTimeOffset Option2 { get; set; }
    public DateTimeOffset Option3 { get; set; }

    public int DurationMinutes { get; set; } = 60;

    // Admin/Tutor chooses final scheduled slot
    public DateTimeOffset? ScheduledStart { get; set; }
    public DateTimeOffset? ScheduledEnd { get; set; }

    public LessonStatus Status { get; set; } = LessonStatus.Pending;

    // Decision (admin/staff)
    public DateTimeOffset? DecisionAtUtc { get; set; }
    public long? DecisionByUserId { get; set; }
    public string? DecisionNote { get; set; }

    // Reschedule
    public bool RescheduleRequested { get; set; } = false;
    public DateTimeOffset? RescheduleRequestedAt { get; set; }
    public string? RescheduleNote { get; set; }
    public int RescheduleCount { get; set; } = 0;

    // Cancellation
    public bool CancellationRequested { get; set; } = false; // tutor-initiated request that needs admin
    public DateTimeOffset? CancellationRequestedAt { get; set; }
    public long? CancellationRequestedByUserId { get; set; }
    public string? CancellationNote { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }
    public long? CancelledByUserId { get; set; }
    public string? CancelReason { get; set; }

    // Zoom placeholders (Phase 4 will populate)
    public string? ZoomMeetingId { get; set; }
    public string? ZoomJoinUrl { get; set; }
    public string? ZoomPassword { get; set; }

    // Session tracking
    public DateTimeOffset? SessionStartedAt { get; set; }
    public DateTimeOffset? SessionEndedAt { get; set; }
    public DateTimeOffset? StudentJoinedAt { get; set; }
    public DateTimeOffset? TutorJoinedAt { get; set; }
    public bool StudentAttended { get; set; } = false;
    public bool TutorAttended { get; set; } = false;
    public long? EndedByAdminUserId { get; set; }

    // Attendance notes (optional; for missing class reports)
    public string? AttendanceNote { get; set; }
}


