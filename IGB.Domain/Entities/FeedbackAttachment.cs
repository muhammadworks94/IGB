using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class FeedbackAttachment : BaseEntity
{
    public long StudentFeedbackId { get; set; }
    public StudentFeedback? StudentFeedback { get; set; }

    public string Kind { get; set; } = string.Empty; // "TestResults" | "Homework"

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty; // relative under wwwroot/uploads/feedback/
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}


