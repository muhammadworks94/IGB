using IGB.Domain.Enums;
using IGB.Domain.Interfaces;
using IGB.Shared.Common;
using Microsoft.Extensions.Logging;

namespace IGB.Application.Services;

public class ApprovalService : IApprovalService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(IUserRepository userRepository, IUnitOfWork unitOfWork, ILogger<ApprovalService> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> ApproveUserAsync(long userId, long approvedByUserId, string? note = null, CancellationToken cancellationToken = default)
    {
        return await SetStatusAsync(userId, approvedByUserId, UserApprovalStatus.Approved, note, cancellationToken);
    }

    public async Task<Result> RejectUserAsync(long userId, long approvedByUserId, string? note = null, CancellationToken cancellationToken = default)
    {
        return await SetStatusAsync(userId, approvedByUserId, UserApprovalStatus.Rejected, note, cancellationToken);
    }

    private async Task<Result> SetStatusAsync(long userId, long approvedByUserId, UserApprovalStatus status, string? note, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return Result.Failure("User not found.");
            }

            if (!user.EmailConfirmed)
            {
                return Result.Failure("User must confirm email before approval.");
            }

            user.ApprovalStatus = status;
            user.ApprovedByUserId = approvedByUserId;
            user.ApprovedAt = DateTime.UtcNow;
            user.ApprovalNote = note;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} status set to {Status} by {ApproverId}", userId, status, approvedByUserId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating approval status for user {UserId}", userId);
            return Result.Failure("An error occurred while updating approval status.");
        }
    }
}


