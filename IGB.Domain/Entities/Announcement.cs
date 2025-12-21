using IGB.Domain.Common;
using IGB.Domain.Enums;

namespace IGB.Domain.Entities;

public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.System;

    // Optional targeting
    public long? TargetStudentUserId { get; set; }
    public User? TargetStudentUser { get; set; }

    public long? TargetTutorUserId { get; set; }
    public User? TargetTutorUser { get; set; }

    public long? TargetGuardianUserId { get; set; }
    public User? TargetGuardianUser { get; set; }

    // Publishing controls
    public bool IsPublished { get; set; } = true;
    public DateTimeOffset PublishAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    // Audit
    public long? CreatedByUserId { get; set; }
}


