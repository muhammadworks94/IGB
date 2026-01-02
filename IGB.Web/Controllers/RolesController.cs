using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[RequirePermission(PermissionCatalog.Permissions.RolesManage)]
public class RolesController : Controller
{
    private readonly ApplicationDbContext _db;

    public RolesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var roles = await _db.RbacRoles.AsNoTracking()
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);
        return View(roles);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long? id, CancellationToken ct)
    {
        var role = id.HasValue
            ? await _db.RbacRoles.Include(r => r.RolePermissions.Where(rp => !rp.IsDeleted)).FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            : new IGB.Domain.Entities.RbacRole { IsSystem = false };

        if (role == null) return NotFound();

        var allPerms = await _db.RbacPermissions.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.Category).ThenBy(p => p.Key).ToListAsync(ct);
        var selected = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        ViewBag.AllPermissions = allPerms;
        ViewBag.SelectedPermissionIds = selected;
        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long? id, string name, string? description, bool isSystem, long[] permissionIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Role name is required.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var role = id.HasValue
            ? await _db.RbacRoles.Include(r => r.RolePermissions.Where(rp => !rp.IsDeleted)).FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct)
            : null;

        if (role == null)
        {
            role = new IGB.Domain.Entities.RbacRole { CreatedAt = DateTime.UtcNow };
            _db.RbacRoles.Add(role);
        }

        role.Name = name.Trim();
        role.Description = description?.Trim();
        role.IsSystem = isSystem;
        role.UpdatedAt = DateTime.UtcNow;

        var keep = permissionIds.ToHashSet();
        var existing = role.RolePermissions.ToList();
        foreach (var rp in existing)
        {
            if (!keep.Contains(rp.PermissionId))
            {
                rp.IsDeleted = true;
                rp.UpdatedAt = DateTime.UtcNow;
            }
        }
        foreach (var pid in keep)
        {
            if (existing.Any(x => x.PermissionId == pid && !x.IsDeleted)) continue;
            role.RolePermissions.Add(new IGB.Domain.Entities.RbacRolePermission { PermissionId = pid, CreatedAt = DateTime.UtcNow });
        }

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Role saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var role = await _db.RbacRoles.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);
        if (role == null) return NotFound();
        role.IsDeleted = true;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Role deleted.";
        return RedirectToAction(nameof(Index));
    }
}


