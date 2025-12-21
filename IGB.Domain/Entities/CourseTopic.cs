using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class CourseTopic : BaseEntity
{
    public long CourseId { get; set; }
    public Course? Course { get; set; }

    public long? ParentTopicId { get; set; }
    public CourseTopic? ParentTopic { get; set; }

    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;

    public ICollection<CourseTopic> SubTopics { get; set; } = new List<CourseTopic>();
}


