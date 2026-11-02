using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Parsing;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using Nexo.Application.Messaging;
using static Nexo.Application.Messaging.MessageFormatter;

namespace Nexo.Application.Transactions;

public class MessageProcessingService : ITransactionRegistrar
{
    private readonly ITransactionMessageParser _parser;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IInstallmentPlanRepository _installmentPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBudgetAlertService _budgetAlertService;

    public MessageProcessingService(
        ITransactionMessageParser parser,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IInstallmentPlanRepository installmentPlanRepository,
        IUnitOfWork unitOfWork,
        IBudgetAlertService budgetAlertService)
    {
        _parser = parser;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _installmentPlanRepository = installmentPlanRepository;
        _unitOfWork = unitOfWork;
        _budgetAlertService = budgetAlertService;
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

        return await RegisterAsync(phoneNumber, parsed, cancellationToken);
    }

    // Registra uma transação já interpretada (pela IA ou por outro meio). Compra com mais de uma
    // parcela vira um InstallmentPlan, que gera as transações de cada parcela.
    public async Task<RegisterMessageResult> RegisterAsync(
        string phoneNumber, ParsedTransaction parsed, CancellationToken cancellationToken = default)
    {
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
            var installmentReply =
                $"✅ *Compra parcelada registrada*\n{parsed.Category.ToEmoji()} {parsed.Category.ToDisplayName()} · {Money(parsed.Amount)}\n"
                + $"📝 {parsed.Description}\n💳 {parsed.InstallmentCount}x de {Money(installmentValue)}";
            return new RegisterMessageResult(true,
                await AppendBudgetAlertAsync(installmentReply, user.Id, parsed, cancellationToken),
                Buttons: InteractiveCommands.AfterRegistration);
        }

        var transaction = new Transaction(
            user.Id, parsed.Type, parsed.Amount, parsed.Category, parsed.Description, parsed.OccurredAt);

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var heading = parsed.Type == TransactionType.Income ? "💰 *Receita registrada*" : "✅ *Despesa registrada*";
        // Quando a descrição só repete o nome da categoria (ex: "Mercado"), a linha do texto não acrescenta nada.
        var showDescription = !string.Equals(parsed.Description, parsed.Category.ToDisplayName(), StringComparison.OrdinalIgnoreCase);
        var reply =
            $"{heading}\n{parsed.Category.ToEmoji()} {parsed.Category.ToDisplayName()} · {Money(parsed.Amount)}\n"
            + (showDescription ? $"📝 {parsed.Description}\n" : "")
            + $"📅 {parsed.OccurredAt:dd/MM}";
        return new RegisterMessageResult(true, await AppendBudgetAlertAsync(reply, user.Id, parsed, cancellationToken),
            Buttons: InteractiveCommands.AfterRegistration);
    }

    // Só despesas contam para os limites de gasto. O alerta vai junto da resposta, na mesma mensagem.
    private async Task<string> AppendBudgetAlertAsync(
        string reply, Guid userId, ParsedTransaction parsed, CancellationToken cancellationToken)
    {
        if (parsed.Type != TransactionType.Expense)
            return reply;

        var alert = await _budgetAlertService.BuildAlertAsync(userId, parsed.Category, parsed.OccurredAt, cancellationToken);
        return alert is null ? reply : $"{reply}\n\n{alert}";
    }
}
