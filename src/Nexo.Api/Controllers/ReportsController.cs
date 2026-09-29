using Microsoft.AspNetCore.Mvc;
using Nexo.Application.Abstractions;
using Nexo.Application.Charts;
using Nexo.Application.Reports;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/users/{phoneNumber}/reports")]
public class ReportsController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IReportService _reportService;
    private readonly IChartService _chartService;

    public ReportsController(IUserRepository userRepository, IReportService reportService, IChartService chartService)
    {
        _userRepository = userRepository;
        _reportService = reportService;
        _chartService = chartService;
    }

    /// <summary>
    /// Resumo de um mês: receitas, despesas, saldo, gastos por categoria e comparação com o mês anterior.
    /// monthOffset: 0 é o mês atual, -1 o mês passado, e assim por diante.
    /// </summary>
    [HttpGet("monthly-summary")]
    public async Task<ActionResult<MonthlySummary>> GetMonthlySummary(
        string phoneNumber, [FromQuery] int monthOffset = 0, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        return Ok(await _reportService.GetMonthlySummaryAsync(user.Id, monthOffset, DateTime.UtcNow, cancellationToken));
    }

    /// <summary>
    /// Receitas e despesas de cada um dos últimos meses (de 1 a 12), do mais antigo para o atual.
    /// </summary>
    [HttpGet("monthly-series")]
    public async Task<ActionResult<IReadOnlyList<MonthlyPoint>>> GetMonthlySeries(
        string phoneNumber, [FromQuery] int months = 6, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        return Ok(await _reportService.GetMonthlySeriesAsync(user.Id, months, DateTime.UtcNow, cancellationToken));
    }

    /// <summary>
    /// Gráfico (PNG) dos gastos por categoria de um mês. É o mesmo gráfico que o NEXO envia pelo WhatsApp.
    /// </summary>
    [HttpGet("charts/categories")]
    [Produces("image/png")]
    public async Task<IActionResult> GetCategoryChart(
        string phoneNumber, [FromQuery] int monthOffset = 0, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        var chart = await _chartService.BuildCategoryChartAsync(user.Id, monthOffset, DateTime.UtcNow, cancellationToken);
        return chart is null ? NotFound() : File(chart.Png, "image/png");
    }

    /// <summary>
    /// Gráfico (PNG) de receitas e despesas dos últimos meses.
    /// </summary>
    [HttpGet("charts/monthly")]
    [Produces("image/png")]
    public async Task<IActionResult> GetMonthlyChart(
        string phoneNumber, [FromQuery] int months = 6, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
            return NotFound();

        var chart = await _chartService.BuildMonthlyChartAsync(user.Id, months, DateTime.UtcNow, cancellationToken);
        return chart is null ? NotFound() : File(chart.Png, "image/png");
    }
}
