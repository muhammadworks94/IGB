using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class GuardianWard : BaseEntity
{
    public long GuardianUserId { get; set; }
    public User? GuardianUser { get; set; }

    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }
}


