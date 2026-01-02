using System.Security.Claims;
using IGB.Shared.Security;
using IGB.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/rbac")]
public class RbacController : ControllerBase
{
    private readonly IPermissionService _perms;

    public RbacController(IPermissionService perms)
    {
        _perms = perms;
    }

    [HttpGet("me")]
    [Authorize] // cookie auth for MVC pages (works with your existing app)
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdStr, out var userId)) return Unauthorized();

        var perms = await _perms.GetUserPermissionsAsync(userId, ct);
        return Ok(new
        {
            userId,
            role = User.FindFirstValue(ClaimTypes.Role),
            permissions = perms.Where(p => p != "*").OrderBy(p => p).ToArray(),
            isAdmin = perms.Contains("*")
        });
    }
}


