using AppAuthService = IGB.Application.Services.IAuthenticationService;
using IGB.Application.Services;
using IGB.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IGB.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppAuthService _authenticationService;
    private readonly IRegistrationService _registrationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AppAuthService authenticationService,
        IRegistrationService registrationService,
        IConfiguration configuration,
        ILogger<AccountController> logger)
    {
        _authenticationService = authenticationService;
        _registrationService = registrationService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // If already logged in, redirect to home
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authenticationService.LoginAsync(model.Email, model.Password);

        if (!result.IsSuccess || result.Value == null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Invalid login attempt.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Value.Id.ToString()),
            new(ClaimTypes.Name, result.Value.Email),
            new(ClaimTypes.GivenName, result.Value.FullName),
            new(ClaimTypes.Role, result.Value.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        // Store user info in session
        HttpContext.Session.SetString("UserId", result.Value.Id.ToString());
        HttpContext.Session.SetString("UserEmail", result.Value.Email);
        HttpContext.Session.SetString("UserName", result.Value.FullName);
        HttpContext.Session.SetString("UserRole", result.Value.Role);

        _logger.LogInformation("User {Email} logged in successfully", model.Email);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        
        _logger.LogInformation("User logged out");
        
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel { TimeZoneId = TimeZoneInfo.Local.Id });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _registrationService.RegisterAsync(new IGB.Application.DTOs.RegisterUserDto
        {
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Password = model.Password,
            Role = "Student",
            LocalNumber = model.LocalNumber,
            WhatsappNumber = model.WhatsappNumber,
            CountryCode = model.CountryCode,
            TimeZoneId = model.TimeZoneId
        }, cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to register.");
            return View(model);
        }

        var (userId, email, token) = result.Value;
        var autoConfirm = _configuration.GetValue<bool>("Auth:AutoConfirmEmails");
        if (autoConfirm)
        {
            // Dev-friendly: auto confirm immediately.
            await _registrationService.ConfirmEmailAsync(userId, token, cancellationToken);
            TempData["Success"] = "Registration successful. Your email has been auto-confirmed. Your account is pending admin approval.";
        }
        else
        {
            var confirmUrl = Url.Action("ConfirmEmail", "Account", new { userId, token }, Request.Scheme);
            // For now, we log the confirmation link (next step: SMTP sender).
            _logger.LogInformation("Email confirmation link for {Email}: {Link}", email, confirmUrl);
            TempData["Success"] = "Registration successful. Please check your email to confirm your account. (Dev mode: link is in logs)";
        }
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(long userId, string token, CancellationToken cancellationToken)
    {
        var result = await _registrationService.ConfirmEmailAsync(userId, token, cancellationToken);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "Invalid confirmation link.";
            return RedirectToAction(nameof(Login));
        }

        TempData["Success"] = "Email confirmed successfully. Your account is now pending admin approval.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdValue, out var userId))
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _authenticationService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to change password.");
            return View(model);
        }

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Index", "Home");
    }
}

