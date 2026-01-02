using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class RbacRolePermission : BaseEntity
{
    public long RoleId { get; set; }
    public RbacRole? Role { get; set; }

    public long PermissionId { get; set; }
    public RbacPermission? Permission { get; set; }
}


