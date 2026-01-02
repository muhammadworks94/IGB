using IGB.Domain.Common;
using IGB.Domain.Enums;

namespace IGB.Domain.Entities;

public class CreditTransaction : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    // Positive adds credits, negative consumes credits
    public int Amount { get; set; }
    public CreditTransactionType Type { get; set; } = CreditTransactionType.Adjustment;

    public string? ReferenceType { get; set; } // e.g. "CourseBooking", "LessonBooking"
    public long? ReferenceId { get; set; }

    public string Reason { get; set; } = string.Empty; // free text
    public string? Notes { get; set; }

    public int BalanceAfter { get; set; } // remaining after applying this tx
    public long? CreatedByUserId { get; set; } // admin/system
}


