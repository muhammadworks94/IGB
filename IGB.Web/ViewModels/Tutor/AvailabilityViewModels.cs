using System.ComponentModel.DataAnnotations;

namespace IGB.Web.ViewModels.Tutor;

public class TutorAvailabilityPageViewModel
{
    public string TutorTimeZoneId { get; set; } = "UTC";
    public List<RuleItem> Rules { get; set; } = new();
    public List<BlockItem> Blocks { get; set; } = new();

    public record RuleItem(long Id, int DayOfWeek, int StartMinutes, int EndMinutes, int SlotMinutes, bool IsActive);
    public record BlockItem(long Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Reason);
}

public class TutorAvailabilityRuleInput
{
    public long? Id { get; set; }

    [Range(0, 6)]
    public int DayOfWeek { get; set; }

    [Range(0, 24 * 60 - 1)]
    public int StartMinutes { get; set; }

    [Range(1, 24 * 60)]
    public int EndMinutes { get; set; }

    [Range(30, 180)]
    public int SlotMinutes { get; set; } = 60;

    public bool IsActive { get; set; } = true;
}

public class TutorAvailabilityBlockInput
{
    public long? Id { get; set; }

    [Required]
    public DateTimeOffset StartUtc { get; set; }

    [Required]
    public DateTimeOffset EndUtc { get; set; }

    public string? Reason { get; set; }
}


