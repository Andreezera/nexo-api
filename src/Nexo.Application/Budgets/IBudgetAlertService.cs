using Nexo.Domain.Enums;

namespace Nexo.Application.Budgets;

public interface IBudgetAlertService
{
    Task<BudgetStatus?> GetStatusAsync(
        Guid userId, TransactionCategory category, DateTime referenceDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetStatus>> GetAllStatusesAsync(
        Guid userId, DateTime referenceDate, CancellationToken cancellationToken = default);

    // Retorna o texto do alerta, ou null se a categoria não tem limite ou ainda está longe de estourar.
    Task<string?> BuildAlertAsync(
        Guid userId, TransactionCategory category, DateTime referenceDate, CancellationToken cancellationToken = default);
}
