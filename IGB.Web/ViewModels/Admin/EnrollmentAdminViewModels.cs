using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Admin;

public class PendingEnrollmentsViewModel
{
    public string? Query { get; set; }
    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "Pending", "Enrollments");
    public List<Row> Items { get; set; } = new();

    public record Row(
        long BookingId,
        string StudentName,
        string CourseName,
        int CreditCost,
        int RemainingCredits,
        DateTime RequestedAtUtc
    );
}


