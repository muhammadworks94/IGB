using System.Security.Claims;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.ViewModels.Credits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize]
public class CourseCreditsController : Controller
{
    private readonly ApplicationDbContext _db;

    public CourseCreditsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // Admin: view any student's ledger for a course
    [Authorize(Policy = "AdminOnly")]
    [RequirePermission(PermissionCatalog.Permissions.CreditsView)]
    public async Task<IActionResult> Ledger(long courseId, long studentUserId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        return await BuildLedgerView(courseId, studentUserId, isAdmin: true, page, pageSize, ct);
    }

    // Student: view own ledger for a course
    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.CreditsView)]
    public async Task<IActionResult> MyLedger(long courseId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var studentId)) return Forbid();
        return await BuildLedgerView(courseId, studentId, isAdmin: false, page, pageSize, ct);
    }

    private async Task<IActionResult> BuildLedgerView(long courseId, long studentUserId, bool isAdmin, int page, int pageSize, CancellationToken ct)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course == null) return NotFound();

        var student = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentUserId && !u.IsDeleted, ct);
        if (student == null) return NotFound();

        var ledger = await _db.CourseCreditLedgers.AsNoTracking()
            .FirstOrDefaultAsync(l => !l.IsDeleted && l.CourseId == courseId && l.StudentUserId == studentUserId, ct);

        ledger ??= new IGB.Domain.Entities.CourseCreditLedger
        {
            CourseId = courseId,
            StudentUserId = studentUserId,
            CreditsAllocated = 0,
            CreditsUsed = 0,
            CreditsRemaining = 0
        };

        var txQuery = _db.CourseLedgerTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.CourseId == courseId && t.StudentUserId == studentUserId)
            .OrderByDescending(t => t.CreatedAt);

        var total = await txQuery.CountAsync(ct);
        var txs = await txQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var vm = new CourseLedgerViewModel
        {
            CourseId = course.Id,
            CourseName = course.Name,
            IsAdminView = isAdmin,
            StudentUserId = studentUserId,
            StudentName = $"{student.FirstName} {student.LastName}".Trim(),
            CreditsAllocated = ledger.CreditsAllocated,
            CreditsUsed = ledger.CreditsUsed,
            CreditsRemaining = ledger.CreditsRemaining,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: isAdmin ? "Ledger" : "MyLedger", Controller: "CourseCredits", RouteValues: isAdmin ? new { courseId, studentUserId } : new { courseId }),
            Transactions = txs.Select(t => new CourseLedgerViewModel.TxRow(t.CreatedAt, t.Type, t.Amount, t.Notes, t.ReferenceId)).ToList()
        };

        return View("Ledger", vm);
    }
}


