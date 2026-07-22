using Nexo.Application.Abstractions;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NexoDbContext _context;

    public UnitOfWork(NexoDbContext context) => _context = context;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
