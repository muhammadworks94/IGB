using IGB.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using IGB.Application.DTOs;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRegistrationService _registrationService;
    private readonly IAuthTokenService _authTokenService;
    private readonly IConfiguration _config;
    private readonly IGB.Web.Services.AdminDashboardRealtimeBroadcaster _rt;

    public AuthController(
        IAuthenticationService authenticationService,
        IJwtTokenService jwtTokenService,
        IRegistrationService registrationService,
        IAuthTokenService authTokenService,
        IConfiguration config,
        IGB.Web.Services.AdminDashboardRealtimeBroadcaster rt)
    {
        _authenticationService = authenticationService;
        _jwtTokenService = jwtTokenService;
        _registrationService = registrationService;
        _authTokenService = authTokenService;
        _config = config;
        _rt = rt;
    }

    public record LoginRequest(string Email, string Password, bool RememberMe = false);
    public record LoginResponse(string AccessToken, string Email, string Role, string FullName);

    public record RegisterRequest(string Email, string Password, string FirstName, string LastName, string? LocalNumber = null, string? WhatsappNumber = null, string? CountryCode = null, string? TimeZoneId = null);
    public record RegisterResponse(long UserId, string Email, bool RequiresEmailConfirmation, string? DevConfirmationUrl = null);

    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(long UserId, string Token, string NewPassword);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authenticationService.LoginAsync(request.Email, request.Password);
        if (result.IsFailure || result.Value == null)
        {
            return Unauthorized(new { error = result.Error ?? "Invalid credentials." });
        }

        // Access token (short-lived)
        var accessToken = _jwtTokenService.GenerateToken(result.Value);

        // Refresh token (stored as HttpOnly cookie)
        var refresh = await _authTokenService.IssueRefreshTokenAsync(result.Value.Id, request.RememberMe);
        if (refresh.IsFailure)
        {
            return StatusCode(500, new { error = refresh.Error ?? "Unable to create refresh token." });
        }

        SetRefreshCookie(refresh.Value.RefreshToken, refresh.Value.ExpiresAt, request.RememberMe);
        return Ok(new LoginResponse(accessToken, result.Value.Email, result.Value.Role, result.Value.FullName));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var reg = await _registrationService.RegisterAsync(new RegisterUserDto
        {
            Email = request.Email,
            Password = request.Password,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = "Student",
            LocalNumber = request.LocalNumber,
            WhatsappNumber = request.WhatsappNumber,
            CountryCode = request.CountryCode,
            TimeZoneId = request.TimeZoneId
        }, ct);

        if (reg.IsFailure)
            return BadRequest(new { error = reg.Error ?? "Unable to register." });

        var (userId, email, token) = reg.Value;
        await _rt.SendToAdminsAsync("user:registered", new
        {
            timeUtc = DateTimeOffset.UtcNow.ToString("O"),
            relative = "Just now",
            type = "User",
            badge = "purple",
            text = $"New student registered: {request.FirstName} {request.LastName}",
            url = $"/AdminUsers/Students?q={Uri.EscapeDataString(request.Email)}"
        }, ct);
        await _rt.SendToAdminsAsync("activity:new", new
        {
            timeUtc = DateTimeOffset.UtcNow.ToString("O"),
            relative = "Just now",
            type = "User",
            badge = "purple",
            text = $"New student registered: {request.FirstName} {request.LastName}",
            url = $"/AdminUsers/Students?q={Uri.EscapeDataString(request.Email)}"
        }, ct);

        var autoConfirm = _config.GetValue<bool>("Auth:AutoConfirmEmails");
        if (autoConfirm)
        {
            await _registrationService.ConfirmEmailAsync(userId, token, ct);
            return Ok(new RegisterResponse(userId, email, RequiresEmailConfirmation: false));
        }

        var confirmUrl = Url.Action("ConfirmEmailApi", "Auth", new { userId, token }, Request.Scheme);
        var devExpose = _config.GetValue<bool>("Auth:ExposeEmailLinksInApiResponse");
        return Ok(new RegisterResponse(userId, email, RequiresEmailConfirmation: true, DevConfirmationUrl: devExpose ? confirmUrl : null));
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmailApi([FromQuery] long userId, [FromQuery] string token, CancellationToken ct)
    {
        var result = await _registrationService.ConfirmEmailAsync(userId, token, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error ?? "Invalid confirmation link." });
        return Ok(new { ok = true });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        // Always return OK (avoid account enumeration)
        var res = await _authTokenService.CreatePasswordResetAsync(request.Email, ct);
        if (res.IsSuccess)
        {
            var (userId, email, token) = res.Value;
            var resetUrl = Url.Action("ResetPassword", "Account", new { userId, token }, Request.Scheme);
            // In production: send via email provider. For now, rely on logs or optionally expose.
            if (_config.GetValue<bool>("Auth:ExposeEmailLinksInApiResponse"))
                return Ok(new { ok = true, devResetUrl = resetUrl });
        }
        return Ok(new { ok = true });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _authTokenService.ResetPasswordAsync(request.UserId, request.Token, request.NewPassword, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error ?? "Unable to reset password." });
        return Ok(new { ok = true });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken ct)
    {
        var refreshCookie = Request.Cookies["igb_refresh"];
        var res = await _authTokenService.RefreshAsync(refreshCookie ?? string.Empty, ct);
        if (res.IsFailure || res.Value.User == null)
            return Unauthorized(new { error = "Invalid refresh token." });

        SetRefreshCookie(res.Value.RefreshToken, res.Value.ExpiresAt, rememberMe: true);
        var accessToken = _jwtTokenService.GenerateToken(res.Value.User);
        return Ok(new LoginResponse(accessToken, res.Value.User.Email, res.Value.User.Role, res.Value.User.FullName));
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var userId))
        {
            await _authTokenService.RevokeRefreshTokenAsync(userId, ct);
        }
        Response.Cookies.Delete("igb_refresh");
        return Ok(new { ok = true });
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

    private void SetRefreshCookie(string token, DateTime expiresAtUtc, bool rememberMe)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = rememberMe ? expiresAtUtc : null // session cookie when not remembering
        };
        Response.Cookies.Append("igb_refresh", token, opts);
    }
}


