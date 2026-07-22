using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Contracts;
using Nexo.Application.Abstractions;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/users/{phoneNumber}/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;

    public TransactionsController(IUserRepository userRepository, ITransactionRepository transactionRepository)
    {
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
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
}
