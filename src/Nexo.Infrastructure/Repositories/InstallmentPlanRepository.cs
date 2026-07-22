using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Repositories;

public class InstallmentPlanRepository : IInstallmentPlanRepository
{
    private readonly NexoDbContext _context;

    public InstallmentPlanRepository(NexoDbContext context) => _context = context;

    public async Task AddAsync(InstallmentPlan plan, CancellationToken cancellationToken = default) =>
        await _context.InstallmentPlans.AddAsync(plan, cancellationToken);
}
