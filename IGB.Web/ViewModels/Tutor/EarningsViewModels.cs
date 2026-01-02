using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Tutor;

public class TutorEarningsViewModel
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int MonthTotal { get; set; }

    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "MyEarnings", "Earnings");
    public List<Row> Items { get; set; } = new();

    public record Row(DateTime CreatedAtUtc, int CreditsEarned, string? Notes, long? LessonBookingId);
}


