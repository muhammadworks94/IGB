using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class CreditLedgerEntry : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    // Positive = add credits, Negative = spend credits
    public int DeltaCredits { get; set; }

    public string Reason { get; set; } = string.Empty; // e.g. "Initial allocation", "Course enrollment"
    public string? ReferenceType { get; set; } // e.g. "CourseBooking"
    public long? ReferenceId { get; set; }

    public long? CreatedByUserId { get; set; } // admin/system
}


