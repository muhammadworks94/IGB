using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class CreditsBalance : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    public int TotalCredits { get; set; } = 0;
    public int UsedCredits { get; set; } = 0;
    public int RemainingCredits { get; set; } = 0;
}


