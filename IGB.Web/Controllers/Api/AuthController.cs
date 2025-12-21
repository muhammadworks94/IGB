using IGB.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(IAuthenticationService authenticationService, IJwtTokenService jwtTokenService)
    {
        _authenticationService = authenticationService;
        _jwtTokenService = jwtTokenService;
    }

    public record LoginRequest(string Email, string Password);
    public record LoginResponse(string Token, string Email, string Role, string FullName);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authenticationService.LoginAsync(request.Email, request.Password);
        if (result.IsFailure || result.Value == null)
        {
            return Unauthorized(new { error = result.Error ?? "Invalid credentials." });
        }

        var token = _jwtTokenService.GenerateToken(result.Value);
        return Ok(new LoginResponse(token, result.Value.Email, result.Value.Role, result.Value.FullName));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            email = User.Identity?.Name,
            role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }
}


