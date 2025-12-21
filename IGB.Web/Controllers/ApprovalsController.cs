using System.Security.Claims;
using AutoMapper;
using IGB.Application.Services;
using IGB.Domain.Interfaces;
using IGB.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IGB.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
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

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var pending = await _userRepository.GetPendingApprovalAsync(cancellationToken);
        var users = _mapper.Map<List<IGB.Application.DTOs.UserDto>>(pending);

        return View(new PendingUsersViewModel { Users = users });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id, string? note, CancellationToken cancellationToken)
    {
        var approverIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(approverIdValue, out var approverId);

        var result = await _approvalService.ApproveUserAsync(id, approverId, note, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "User approved." : (result.Error ?? "Unable to approve user.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, string? note, CancellationToken cancellationToken)
    {
        var approverIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(approverIdValue, out var approverId);

        var result = await _approvalService.RejectUserAsync(id, approverId, note, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "User rejected." : (result.Error ?? "Unable to reject user.");
        return RedirectToAction(nameof(Index));
    }
}


