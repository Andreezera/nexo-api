using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Contracts;
using Nexo.Application.Abstractions;
using Nexo.Domain.Enums;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/users/{phoneNumber}/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TransactionsController(
        IUserRepository userRepository, ITransactionRepository transactionRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Lista as transações já registradas para um usuário, identificado pelo número de telefone.
    /// Usado para demonstrar que os dados extraídos das mensagens foram persistidos corretamente.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionResponse>>> GetByPhoneNumber(
        string phoneNumber, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        var transactions = await _transactionRepository.GetByUserIdAsync(user.Id, cancellationToken);

        var response = transactions
            .Select(t => new TransactionResponse(t.Id, t.Type, t.Amount, t.Category, t.Description, t.OccurredAt))
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Resumo financeiro do usuário: total de receitas, total de despesas e saldo líquido.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<BalanceSummaryResponse>> GetSummary(
        string phoneNumber, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        var transactions = await _transactionRepository.GetByUserIdAsync(user.Id, cancellationToken);

        var totalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var totalExpense = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

        return Ok(new BalanceSummaryResponse(totalIncome, totalExpense, totalIncome - totalExpense));
    }

    /// <summary>
    /// Corrige uma transação lançada com dados errados (valor, categoria, descrição, tipo ou data).
    /// </summary>
    [HttpPut("~/api/transactions/{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id, cancellationToken);
        if (transaction is null)
            return NotFound();

        transaction.Update(request.Type, request.Amount, request.Category, request.Description, request.OccurredAt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new TransactionResponse(
            transaction.Id, transaction.Type, transaction.Amount, transaction.Category, transaction.Description, transaction.OccurredAt));
    }

    /// <summary>
    /// Remove uma transação lançada por engano.
    /// </summary>
    [HttpDelete("~/api/transactions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id, cancellationToken);
        if (transaction is null)
            return NotFound();

        transaction.Delete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
