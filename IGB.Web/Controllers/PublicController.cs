using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers;

[AllowAnonymous]
public sealed class PublicController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        if (User?.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Portal");
        ViewData["Title"] = "Home";
        return View();
    }

    [HttpGet("about-us")]
    public IActionResult About()
    {
        ViewData["Title"] = "About Us";
        return View();
    }

    [HttpGet("services")]
    public IActionResult Services()
    {
        ViewData["Title"] = "Services";
        return View();
    }

    [HttpGet("courses")]
    public IActionResult Courses()
    {
        ViewData["Title"] = "Courses";
        return View();
    }

    [HttpGet("contact-us")]
    public IActionResult Contact()
    {
        ViewData["Title"] = "Contact Us";
        return View();
    }

    [HttpGet("programs/{slug}")]
    public IActionResult Program(string slug)
    {
        ViewData["Title"] = "Programs";
        ViewData["ProgramSlug"] = slug;
        return View("Program");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet("error")]
    public IActionResult Error()
    {
        ViewData["Title"] = "Error";
        return View();
    }
}


