using IGB.Web.ViewModels.Components;

namespace IGB.Web.ViewModels.Credits;

public class MyCreditsViewModel
{
    public int TotalCredits { get; set; }
    public int UsedCredits { get; set; }
    public int RemainingCredits { get; set; }
    public int LowCreditThreshold { get; set; } = 5;

    public PaginationViewModel Pagination { get; set; } = new(1, 10, 0, "My", "Credits");
    public List<TxRow> Transactions { get; set; } = new();

    public record TxRow(DateTime CreatedAtUtc, string Type, int Amount, int BalanceAfter, string Reason, string? Notes);
}


