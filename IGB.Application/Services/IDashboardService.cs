using IGB.Application.DTOs;
using IGB.Shared.Common;

namespace IGB.Application.Services;

public interface IDashboardService
{
    Task<Result<DashboardSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default);
}


