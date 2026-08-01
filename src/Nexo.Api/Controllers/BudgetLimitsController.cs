using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Contracts;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/users/{phoneNumber}/budget-limits")]
public class BudgetLimitsController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IBudgetLimitRepository _budgetLimitRepository;
    private readonly IBudgetAlertService _budgetAlertService;
    private readonly IUnitOfWork _unitOfWork;

    public BudgetLimitsController(
        IUserRepository userRepository,
        IBudgetLimitRepository budgetLimitRepository,
        IBudgetAlertService budgetAlertService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _budgetLimitRepository = budgetLimitRepository;
        _budgetAlertService = budgetAlertService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Lista os limites de gasto do usuário, com quanto já foi gasto em cada categoria no mês atual.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetLimitResponse>>> GetAll(
        string phoneNumber, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        var statuses = await _budgetAlertService.GetAllStatusesAsync(user.Id, DateTime.UtcNow, cancellationToken);
        return Ok(statuses.Select(ToResponse).ToList());
    }

    /// <summary>
    /// Define (ou atualiza) o limite mensal de gasto para uma categoria.
    /// </summary>
    [HttpPut("{category}")]
    public async Task<ActionResult<BudgetLimitResponse>> Set(
        string phoneNumber, TransactionCategory category, SetBudgetLimitRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        if (request.MonthlyLimit <= 0)
            return BadRequest("O limite mensal deve ser maior que zero.");

        var limit = await _budgetLimitRepository.GetAsync(user.Id, category, cancellationToken);
        if (limit is null)
        {
            limit = new BudgetLimit(user.Id, category, request.MonthlyLimit);
            await _budgetLimitRepository.AddAsync(limit, cancellationToken);
        }
        else
        {
            limit.UpdateLimit(request.MonthlyLimit);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var status = await _budgetAlertService.GetStatusAsync(user.Id, category, DateTime.UtcNow, cancellationToken);
        return Ok(ToResponse(status!));
    }

    /// <summary>
    /// Remove o limite de gasto de uma categoria.
    /// </summary>
    [HttpDelete("{category}")]
    public async Task<IActionResult> Delete(
        string phoneNumber, TransactionCategory category, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        var limit = await _budgetLimitRepository.GetAsync(user.Id, category, cancellationToken);
        if (limit is null)
            return NotFound();

        _budgetLimitRepository.Remove(limit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static BudgetLimitResponse ToResponse(BudgetStatus status) => new(
        status.Category,
        status.MonthlyLimit,
        status.Spent,
        Math.Round(status.PercentUsed * 100, 1),
        status.PercentUsed >= 1m);
}
