using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers;

[Authorize]
public sealed class PortalController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        if (User.IsInRole("SuperAdmin")) return RedirectToAction("SuperAdmin");
        if (User.IsInRole("Admin")) return RedirectToAction("Admin");
        if (User.IsInRole("Tutor")) return RedirectToAction("Tutor");
        if (User.IsInRole("Student")) return RedirectToAction("Student");
        if (User.IsInRole("Guardian")) return RedirectToAction("Guardian");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult SuperAdmin() => View();

    [HttpGet]
    public IActionResult Admin() => View();

    [HttpGet]
    public IActionResult Tutor() => View();

    [HttpGet]
    public IActionResult Student() => View();

    [HttpGet]
    public IActionResult Guardian() => View();
}


