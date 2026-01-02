using System.Security.Claims;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.Services;
using IGB.Web.ViewModels.Credits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IGB.Web.Options;

namespace IGB.Web.Controllers;

[Authorize]
public class CreditsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CreditService _credits;
    private readonly IOptionsMonitor<CreditPolicyOptions> _opt;

    public CreditsController(ApplicationDbContext db, CreditService credits, IOptionsMonitor<CreditPolicyOptions> opt)
    {
        _db = db;
        _credits = credits;
        _opt = opt;
    }

    // Student: My Credits
    [Authorize(Roles = "Student")]
    [RequirePermission(PermissionCatalog.Permissions.CreditsView)]
    public async Task<IActionResult> My(int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var userId)) return Forbid();

        var bal = await _credits.GetOrCreateBalanceAsync(userId, ct);

        var query = _db.CreditTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var vm = new MyCreditsViewModel
        {
            TotalCredits = bal.TotalCredits,
            UsedCredits = bal.UsedCredits,
            RemainingCredits = bal.RemainingCredits,
            LowCreditThreshold = _opt.CurrentValue.LowCreditThreshold,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "My", Controller: "Credits"),
            Transactions = items.Select(t => new MyCreditsViewModel.TxRow(t.CreatedAt, t.Type.ToString(), t.Amount, t.BalanceAfter, t.Reason, t.Notes)).ToList()
        };

        return View(vm);
    }

    // Optional: API endpoint for realtime polling/fetch
    [HttpGet]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var userId)) return Unauthorized();

        var bal = await _credits.GetOrCreateBalanceAsync(userId, ct);
        return Ok(new { total = bal.TotalCredits, used = bal.UsedCredits, remaining = bal.RemainingCredits });
    }
}


