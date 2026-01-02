namespace IGB.Web.ViewModels;

public class UsersUserRolesViewModel
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;

    public List<RoleItem> AllRoles { get; set; } = new();
    public HashSet<long> SelectedRoleIds { get; set; } = new();

    public record RoleItem(long Id, string Name, bool IsSystem);
}


