using System.ComponentModel.DataAnnotations;
using IGB.Domain.Enums;

namespace IGB.Web.ViewModels;

public sealed class AnnouncementListItemViewModel
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AnnouncementAudience Audience { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset PublishAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

public sealed class AnnouncementEditViewModel
{
    public long? Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(5000)]
    public string Body { get; set; } = string.Empty;

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.System;

    // optional targeting
    public long? TargetStudentUserId { get; set; }
    public long? TargetTutorUserId { get; set; }
    public long? TargetGuardianUserId { get; set; }

    public bool IsPublished { get; set; } = true;
    public DateTimeOffset PublishAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}


