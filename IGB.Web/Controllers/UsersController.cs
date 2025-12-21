using IGB.Application.DTOs;
using IGB.Application.Services;
using IGB.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IGB.Shared.DTOs;

namespace IGB.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        var result = await _userService.GetAllAsync(page, pageSize);
        
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "An error occurred while loading users";
            return View(new PagedResult<UserDto>());
        }
        
        return View(result.Value);
    }

    public async Task<IActionResult> Details(long id)
    {
        var result = await _userService.GetByIdAsync(id);
        
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "User not found";
            return RedirectToAction(nameof(Index));
        }
        
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var dto = new CreateUserDto
        {
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Password = model.Password,
            Role = model.Role,
            PhoneNumber = model.PhoneNumber
        };

        var result = await _userService.CreateAsync(dto);
        
        if (result.IsFailure)
        {
            ModelState.AddModelError("", result.Error ?? "An error occurred while creating the user");
            if (result.Errors != null)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error);
                }
            }
            return View(model);
        }
        
        TempData["Success"] = "User created successfully";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var result = await _userService.GetByIdAsync(id);
        
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "User not found";
            return RedirectToAction(nameof(Index));
        }

        var viewModel = new EditUserViewModel
        {
            Id = result.Value!.Id,
            FirstName = result.Value!.FirstName,
            LastName = result.Value!.LastName,
            PhoneNumber = result.Value!.PhoneNumber,
            IsActive = result.Value!.IsActive
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, EditUserViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
            return View(model);

        var dto = new UpdateUserDto
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            PhoneNumber = model.PhoneNumber,
            IsActive = model.IsActive
        };

        var result = await _userService.UpdateAsync(id, dto);
        
        if (result.IsFailure)
        {
            ModelState.AddModelError("", result.Error ?? "An error occurred while updating the user");
            return View(model);
        }
        
        TempData["Success"] = "User updated successfully";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _userService.DeleteAsync(id);
        
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "An error occurred while deleting the user";
        }
        else
        {
            TempData["Success"] = "User deleted successfully";
        }
        
        return RedirectToAction(nameof(Index));
    }
}

