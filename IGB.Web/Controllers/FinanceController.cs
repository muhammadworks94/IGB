using System.Security.Claims;
using IGB.Domain.Enums;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.Services;
using IGB.Web.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[RequirePermission(PermissionCatalog.Permissions.CreditsManage)]
public class FinanceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CreditService _credits;

    public FinanceController(ApplicationDbContext db, CreditService credits)
    {
        _db = db;
        _credits = credits;
    }

    [HttpGet]
    public async Task<IActionResult> Allocate(long? studentUserId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        AllocateCreditsViewModel vm = new();

        if (studentUserId.HasValue)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentUserId.Value && !u.IsDeleted, ct);
            if (user != null)
            {
                var bal = await _credits.GetOrCreateBalanceAsync(user.Id, ct);
                vm.StudentUserId = user.Id;
                vm.StudentName = $"{user.FirstName} {user.LastName}".Trim();
                vm.CurrentRemaining = bal.RemainingCredits;

                var query = _db.CreditTransactions.AsNoTracking()
                    .Where(t => !t.IsDeleted && t.UserId == user.Id)
                    .OrderByDescending(t => t.CreatedAt);

                var total = await query.CountAsync(ct);
                var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

                vm.Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "Allocate", Controller: "Finance", RouteValues: new { studentUserId = user.Id });
                vm.RecentTransactions = items.Select(t => new AllocateCreditsViewModel.TxRow(
                    t.CreatedAt,
                    t.Type.ToString(),
                    t.Amount,
                    t.BalanceAfter,
                    t.Reason,
                    t.Notes
                )).ToList();
            }
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Allocate(AllocateCreditsViewModel model, CancellationToken ct)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(adminIdStr, out var adminId);

        if (!model.StudentUserId.HasValue || model.StudentUserId.Value <= 0)
            ModelState.AddModelError(nameof(model.StudentUserId), "Select a student.");
        if (model.Amount == 0)
            ModelState.AddModelError(nameof(model.Amount), "Amount is required.");

        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Allocate), new { studentUserId = model.StudentUserId ?? 0 });

        var amount = model.Amount;
        // Purchase/Bonus/Refund should be positive; Adjustment can be +/-.
        if (model.ReasonType is CreditTransactionType.Purchase or CreditTransactionType.Bonus or CreditTransactionType.Refund)
            amount = Math.Abs(amount);

        var studentId = model.StudentUserId ?? 0;
        if (studentId <= 0) return RedirectToAction(nameof(Allocate));

        await _credits.AddWalletTransactionAsync(
            userId: studentId,
            amount: amount,
            type: model.ReasonType,
            reason: model.ReasonType.ToString(),
            notes: model.Notes,
            referenceType: "AdminAllocate",
            referenceId: null,
            createdByUserId: adminId > 0 ? adminId : null,
            ct: ct);

        TempData["Success"] = "Credits updated.";
        return RedirectToAction(nameof(Allocate), new { studentUserId = studentId });
    }

    // Autocomplete students
    [HttpGet]
    public async Task<IActionResult> StudentLookup(string q, CancellationToken ct)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return Ok(Array.Empty<object>());

        var users = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student" && (u.FirstName.Contains(q) || u.LastName.Contains(q) || u.Email.Contains(q)))
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Take(10)
            .Select(u => new { id = u.Id, name = (u.FirstName + " " + u.LastName).Trim(), email = u.Email })
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(long studentUserId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentUserId && !u.IsDeleted, ct);
        if (user == null) return NotFound();

        var txs = await _db.CreditTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.UserId == studentUserId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,Type,Amount,BalanceAfter,Reason,Notes");
        foreach (var t in txs)
        {
            string esc(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
            sb.AppendLine($"{t.CreatedAt:yyyy-MM-dd HH:mm},{esc(t.Type.ToString())},{t.Amount},{t.BalanceAfter},{esc(t.Reason)},{esc(t.Notes ?? "")}");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"credits_{user.Email}.csv");
    }
}


