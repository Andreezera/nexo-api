namespace Nexo.Application.Projections;

public interface IPurchaseProjectionService
{
    Task<PurchaseProjection> ProjectAsync(
        Guid userId, decimal price, int installmentCount, DateTime referenceDate, CancellationToken cancellationToken = default);
}
