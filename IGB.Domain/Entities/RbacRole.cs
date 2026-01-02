using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class RbacRole : BaseEntity
{
    public string Name { get; set; } = string.Empty; // unique
    public string? Description { get; set; }
    public bool IsSystem { get; set; } = false;

    public ICollection<RbacRolePermission> RolePermissions { get; set; } = new List<RbacRolePermission>();
    public ICollection<UserRbacRole> UserRoles { get; set; } = new List<UserRbacRole>();
}


