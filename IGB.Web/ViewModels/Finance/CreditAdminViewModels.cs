using IGB.Domain.Enums;
using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Finance;

public class AllocateCreditsViewModel
{
    public long? StudentUserId { get; set; }
    public string? StudentName { get; set; }
    public int? CurrentRemaining { get; set; }

    public int Amount { get; set; } = 0;
    public CreditTransactionType ReasonType { get; set; } = CreditTransactionType.Purchase;
    public string? Notes { get; set; }

    public List<TxRow> RecentTransactions { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "Allocate", "Finance");

    public record TxRow(DateTime CreatedAtUtc, string Type, int Amount, int BalanceAfter, string Reason, string? Notes);
}


