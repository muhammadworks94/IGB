using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Credits;

public class CourseLedgerViewModel
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;

    public bool IsAdminView { get; set; }
    public long? StudentUserId { get; set; }
    public string? StudentName { get; set; }

    public int CreditsAllocated { get; set; }
    public int CreditsUsed { get; set; }
    public int CreditsRemaining { get; set; }

    public List<TxRow> Transactions { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "Ledger", "CourseCredits");

    public record TxRow(DateTime CreatedAtUtc, string Type, int Amount, string? Notes, long? ReferenceId);
}


