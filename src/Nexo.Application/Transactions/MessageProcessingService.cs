using Nexo.Application.Abstractions;
using Nexo.Application.Parsing;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Application.Transactions;

public class MessageProcessingService
{
    private readonly ITransactionMessageParser _parser;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IInstallmentPlanRepository _installmentPlanRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MessageProcessingService(
        ITransactionMessageParser parser,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IInstallmentPlanRepository installmentPlanRepository,
        IUnitOfWork unitOfWork)
    {
        _parser = parser;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _installmentPlanRepository = installmentPlanRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterMessageResult> ProcessAsync(
        string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var parsed = await _parser.ParseAsync(message, cancellationToken);

        if (parsed is null)
        {
            return new RegisterMessageResult(false,
                "Não consegui entender essa mensagem. Tente algo como \"gastei 30 reais no mercado\" ou \"ganhei 500 reais hoje\".");
        }

        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            user = new User(phoneNumber);
            await _userRepository.AddAsync(user, cancellationToken);
        }

        if (parsed.InstallmentCount is > 1)
        {
            var plan = InstallmentPlan.Create(
                user.Id, parsed.Description, parsed.Category, parsed.Amount, parsed.InstallmentCount.Value, parsed.OccurredAt);

            await _installmentPlanRepository.AddAsync(plan, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var installmentValue = plan.Transactions.First().Amount;
            return new RegisterMessageResult(true,
                $"Compra parcelada registrada: {parsed.Description}, em {parsed.InstallmentCount}x de R$ {installmentValue:F2}.");
        }

        var transaction = new Transaction(
            user.Id, parsed.Type, parsed.Amount, parsed.Category, parsed.Description, parsed.OccurredAt);

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var label = parsed.Type == TransactionType.Income ? "Receita" : "Despesa";
        return new RegisterMessageResult(true, $"{label} registrada: R$ {parsed.Amount:F2} em {parsed.Category}.");
    }
}
