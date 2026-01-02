using System.Security.Claims;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.ViewModels.Tutor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Roles = "Tutor")]
[RequirePermission(PermissionCatalog.Permissions.EarningsView)]
public class EarningsController : Controller
{
    private readonly ApplicationDbContext _db;

    public EarningsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> MyEarnings(int year = 0, int month = 0, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var now = DateTime.UtcNow;
        year = year <= 0 ? now.Year : year;
        month = month is < 1 or > 12 ? now.Month : month;

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);

        var query = _db.TutorEarningTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.TutorUserId == tutorId && t.CreatedAt >= from && t.CreatedAt < to)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
        var monthTotal = await query.SumAsync(t => (int?)t.CreditsEarned, ct) ?? 0;

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return View(new TutorEarningsViewModel
        {
            Year = year,
            Month = month,
            MonthTotal = monthTotal,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(page, pageSize, total, Action: "MyEarnings", Controller: "Earnings", RouteValues: new { year, month }),
            Items = items.Select(t => new TutorEarningsViewModel.Row(t.CreatedAt, t.CreditsEarned, t.Notes, t.LessonBookingId)).ToList()
        });
    }

    public async Task<IActionResult> ExportCsv(int year = 0, int month = 0, CancellationToken ct = default)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(uidStr, out var tutorId)) return Forbid();

        var now = DateTime.UtcNow;
        year = year <= 0 ? now.Year : year;
        month = month is < 1 or > 12 ? now.Month : month;

        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);

        var items = await _db.TutorEarningTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.TutorUserId == tutorId && t.CreatedAt >= from && t.CreatedAt < to)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,CreditsEarned,LessonId,Notes");
        foreach (var t in items)
        {
            string esc(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
            sb.AppendLine($"{t.CreatedAt:yyyy-MM-dd HH:mm},{t.CreditsEarned},{t.LessonBookingId},{esc(t.Notes ?? "")}");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"earnings_{year}-{month:00}.csv");
    }
}


