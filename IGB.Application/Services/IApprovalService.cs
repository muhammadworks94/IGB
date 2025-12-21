using IGB.Shared.Common;

namespace IGB.Application.Services;

public interface IApprovalService
{
    Task<Result> ApproveUserAsync(long userId, long approvedByUserId, string? note = null, CancellationToken cancellationToken = default);
    Task<Result> RejectUserAsync(long userId, long approvedByUserId, string? note = null, CancellationToken cancellationToken = default);
}


