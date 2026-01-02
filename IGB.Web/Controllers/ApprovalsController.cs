using System.Security.Claims;
using AutoMapper;
using IGB.Application.Services;
using IGB.Domain.Interfaces;
using IGB.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ApprovalsController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IApprovalService _approvalService;
    private readonly IMapper _mapper;

    public ApprovalsController(IUserRepository userRepository, IApprovalService approvalService, IMapper mapper)
    {
        _userRepository = userRepository;
        _approvalService = approvalService;
        _mapper = mapper;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 200 ? 10 : pageSize;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var total = await _userRepository.GetPendingApprovalCountAsync(q, cancellationToken);
        var pending = await _userRepository.GetPendingApprovalPagedAsync(page, pageSize, q, cancellationToken);
        var users = _mapper.Map<List<IGB.Application.DTOs.UserDto>>(pending);

        return View(new PendingUsersViewModel
        {
            Users = users,
            Query = q,
            Pagination = new IGB.Web.ViewModels.Components.PaginationViewModel(
                page,
                pageSize,
                total,
                Action: "Index",
                Controller: "Approvals",
                RouteValues: new { q }
            )
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id, string? note, string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var approverIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(approverIdValue, out var approverId);

        var result = await _approvalService.ApproveUserAsync(id, approverId, note, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "User approved." : (result.Error ?? "Unable to approve user.");
        return RedirectToAction(nameof(Index), new { q, page, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, string? note, string? q, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var approverIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(approverIdValue, out var approverId);

        var result = await _approvalService.RejectUserAsync(id, approverId, note, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "User rejected." : (result.Error ?? "Unable to reject user.");
        return RedirectToAction(nameof(Index), new { q, page, pageSize });
    }
}


