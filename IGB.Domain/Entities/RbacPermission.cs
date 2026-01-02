using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class RbacPermission : BaseEntity
{
    public string Key { get; set; } = string.Empty; // unique
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<RbacRolePermission> RolePermissions { get; set; } = new List<RbacRolePermission>();
}


