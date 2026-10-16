using Nexo.Application.Abstractions;

namespace Nexo.Application.Recurring;

public interface IRecurringTransactionService
{
    // Cria os lançamentos das contas recorrentes que já venceram neste mês e ainda não foram geradas.
    // Com userId, olha só as contas desse usuário. Devolve quantos lançamentos foram criados.
    Task<int> GenerateDueAsync(DateTime today, Guid? userId = null, CancellationToken cancellationToken = default);
}

public class RecurringTransactionService : IRecurringTransactionService
{
    private readonly IRecurringTransactionRepository _recurringRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecurringTransactionService(
        IRecurringTransactionRepository recurringRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _recurringRepository = recurringRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> GenerateDueAsync(DateTime today, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var candidates = userId is { } id
            ? await _recurringRepository.GetActiveByUserIdAsync(id, cancellationToken)
            : await _recurringRepository.GetAllActiveAsync(cancellationToken);

        var created = 0;
        foreach (var recurring in candidates.Where(r => r.IsDue(today)))
        {
            await _transactionRepository.AddAsync(recurring.GenerateTransaction(today), cancellationToken);
            created++;
        }

        if (created > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return created;
    }
}
