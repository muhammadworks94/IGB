using System.ComponentModel.DataAnnotations;

namespace IGB.Web.ViewModels.Progress;

public class MarkTopicsViewModel
{
    public long LessonId { get; set; }
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; set; }

    public List<TopicItem> Topics { get; set; } = new();
    public HashSet<long> SelectedTopicIds { get; set; } = new();

    public record TopicItem(long Id, string Title, long? ParentId, int SortOrder);
}

public class AddProgressNoteViewModel
{
    public long StudentUserId { get; set; }
    public long CourseId { get; set; }

    [Required, MaxLength(2000)]
    public string Note { get; set; } = string.Empty;
}


