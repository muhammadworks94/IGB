using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class TestReportTopic : BaseEntity
{
    public long TestReportId { get; set; }
    public TestReport? TestReport { get; set; }

    public long CourseTopicId { get; set; }
    public CourseTopic? CourseTopic { get; set; }
}


