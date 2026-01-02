using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/student/dashboard")]
[Authorize(Roles = "Student")]
[RequirePermission(PermissionCatalog.Permissions.LessonsViewOwn)]
public sealed class StudentDashboardController : ControllerBase
{
    private readonly StudentDashboardDataService _svc;

    public StudentDashboardController(StudentDashboardDataService svc)
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


