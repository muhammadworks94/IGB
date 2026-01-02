using Microsoft.AspNetCore.Authorization;

namespace IGB.Web.Security;

public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionKey)
    {
        Policy = $"{PermissionPolicyProvider.Prefix}{permissionKey}";
    }
}


