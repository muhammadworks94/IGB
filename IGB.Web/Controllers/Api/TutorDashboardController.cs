using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/tutor/dashboard")]
[Authorize(Roles = "Tutor")]
[RequirePermission(PermissionCatalog.Permissions.LessonsViewOwn)]
public sealed class TutorDashboardController : ControllerBase
{
    private readonly TutorDashboardDataService _svc;

    public TutorDashboardController(TutorDashboardDataService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var payload = await _svc.GetAsync(User, ct);
        return Ok(payload);
    }
}


