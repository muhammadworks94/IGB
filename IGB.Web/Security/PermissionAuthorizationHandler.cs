using System.Security.Claims;
using IGB.Shared.Security;
using Microsoft.AspNetCore.Authorization;

namespace IGB.Web.Security;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _perms;

    public PermissionAuthorizationHandler(IPermissionService perms)
    {
        _perms = perms;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return;

        // Claims-first (cookie can be enriched with permissions)
        var claimPerms = context.User.FindAll(PermissionCatalog.ClaimType).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (claimPerms.Contains("*") || claimPerms.Contains(requirement.PermissionKey))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdStr, out var userId)) return;

        var perms = await _perms.GetUserPermissionsAsync(userId);
        if (perms.Contains("*") || perms.Contains(requirement.PermissionKey))
            context.Succeed(requirement);
    }
}


