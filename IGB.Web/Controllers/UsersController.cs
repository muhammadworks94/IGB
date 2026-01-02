using IGB.Application.DTOs;
using IGB.Application.Services;
using IGB.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IGB.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 200 ? 10 : pageSize;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var result = await _userService.GetAllAsync(page, pageSize, q);
        
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "An error occurred while loading users";
            return View(new PagedResult<UserDto>());
        }

        ViewBag.Q = q;
        
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

        // Prefer rich "People" profiles for user types where we have full dashboards (courses, lessons, ratings, wards, etc.)
        var role = (result.Value?.Role ?? "").Trim();
        if (role.Equals("Tutor", StringComparison.OrdinalIgnoreCase))
            return Redirect($"/People/Tutor/{id}");
        if (role.Equals("Student", StringComparison.OrdinalIgnoreCase))
            return Redirect($"/People/Student/{id}");
        if (role.Equals("Guardian", StringComparison.OrdinalIgnoreCase))
            return Redirect($"/People/Guardian/{id}");

        return View(result.Value);
    }

    // Assign custom RBAC roles to a user
    [HttpGet]
    public async Task<IActionResult> Roles(long id, CancellationToken ct)
    {
        var userResult = await _userService.GetByIdAsync(id, ct);
        if (userResult.IsFailure) return RedirectToAction(nameof(Index));
        if (userResult.Value == null) return RedirectToAction(nameof(Index));

        var db = HttpContext.RequestServices.GetRequiredService<IGB.Infrastructure.Data.ApplicationDbContext>();
        var allRoles = await db.RbacRoles.AsNoTracking().Where(r => !r.IsDeleted).OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name).ToListAsync(ct);
        var assigned = await db.UserRbacRoles.AsNoTracking().Where(ur => !ur.IsDeleted && ur.UserId == id).Select(ur => ur.RoleId).ToListAsync(ct);

        var vm = new IGB.Web.ViewModels.UsersUserRolesViewModel
        {
            UserId = id,
            Email = userResult.Value.Email,
            AllRoles = allRoles.Select(r => new IGB.Web.ViewModels.UsersUserRolesViewModel.RoleItem(r.Id, r.Name, r.IsSystem)).ToList(),
            SelectedRoleIds = assigned.ToHashSet()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Roles(IGB.Web.ViewModels.UsersUserRolesViewModel model, CancellationToken ct)
    {
        var db = HttpContext.RequestServices.GetRequiredService<IGB.Infrastructure.Data.ApplicationDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == model.UserId && !u.IsDeleted, ct);
        if (user == null) return NotFound();

        var existing = await db.UserRbacRoles.Where(ur => !ur.IsDeleted && ur.UserId == model.UserId).ToListAsync(ct);
        var keep = (model.SelectedRoleIds ?? new HashSet<long>()).ToHashSet();

        foreach (var ur in existing)
        {
            if (!keep.Contains(ur.RoleId))
            {
                ur.IsDeleted = true;
                ur.UpdatedAt = DateTime.UtcNow;
            }
        }

        foreach (var roleId in keep)
        {
            if (existing.Any(x => x.RoleId == roleId && !x.IsDeleted)) continue;
            db.UserRbacRoles.Add(new IGB.Domain.Entities.UserRbacRole { UserId = model.UserId, RoleId = roleId, CreatedAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync(ct);
        TempData["Success"] = "User roles updated.";
        return RedirectToAction(nameof(Details), new { id = model.UserId });
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

