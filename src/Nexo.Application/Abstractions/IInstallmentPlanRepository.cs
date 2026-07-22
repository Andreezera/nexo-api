using Nexo.Domain.Entities;

namespace Nexo.Application.Abstractions;

public interface IInstallmentPlanRepository
{
    Task AddAsync(InstallmentPlan plan, CancellationToken cancellationToken = default);
}
