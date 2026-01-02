namespace IGB.Web.Security;

public interface IPermissionService
{
    Task<HashSet<string>> GetUserPermissionsAsync(long userId, CancellationToken ct = default);
}


