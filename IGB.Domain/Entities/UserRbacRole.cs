using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class UserRbacRole : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    public long RoleId { get; set; }
    public RbacRole? Role { get; set; }
}


