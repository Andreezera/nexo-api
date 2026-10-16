using Nexo.Application.Recurring;

namespace Nexo.Api.Workers;

// Gera os lançamentos das contas recorrentes. Roda ao subir a API e depois de tempos em tempos,
// então uma conta do dia 10 aparece mesmo que a API estivesse desligada nesse dia.
public class RecurringTransactionWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringTransactionWorker> _logger;

    public RecurringTransactionWorker(IServiceScopeFactory scopeFactory, ILogger<RecurringTransactionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRecurringTransactionService>();
                var created = await service.GenerateDueAsync(DateTime.UtcNow, null, stoppingToken);
                if (created > 0)
                    _logger.LogInformation("{Count} lançamento(s) de contas recorrentes gerado(s).", created);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao gerar os lançamentos das contas recorrentes.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
