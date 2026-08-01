using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Contracts;
using Nexo.Application.Abstractions;
using Nexo.Application.Projections;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/users/{phoneNumber}/purchase-projection")]
public class PurchaseProjectionController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPurchaseProjectionService _projectionService;

    public PurchaseProjectionController(IUserRepository userRepository, IPurchaseProjectionService projectionService)
    {
        _userRepository = userRepository;
        _projectionService = projectionService;
    }

    /// <summary>
    /// Responde "consigo comprar X? quando vou ter o valor?". Usa o saldo atual, a média mensal dos
    /// últimos meses e as parcelas que o usuário já tem comprometidas nos meses seguintes.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PurchaseProjectionResponse>> Get(
        string phoneNumber, [FromQuery] decimal price, [FromQuery] int installments = 1,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        if (price <= 0)
            return BadRequest("O valor da compra deve ser maior que zero.");
        if (installments <= 0)
            return BadRequest("O número de parcelas deve ser maior que zero.");

        var projection = await _projectionService.ProjectAsync(user.Id, price, installments, DateTime.UtcNow, cancellationToken);

        return Ok(new PurchaseProjectionResponse(
            projection.Price,
            projection.CurrentBalance,
            projection.AverageMonthlyIncome,
            projection.AverageMonthlyExpense,
            projection.MonthlyMargin,
            projection.CanAffordNow,
            projection.MonthsUntilAffordable,
            projection.AffordableMonth,
            projection.InstallmentCount,
            projection.InstallmentValue,
            projection.FitsMonthlyMargin));
    }
}
