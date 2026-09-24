using Nexo.Application.WhatsApp;

namespace Nexo.Api.WhatsApp;

public class WhatsAppMessageWorker : BackgroundService
{
    private readonly WhatsAppMessageQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppMessageWorker> _logger;

    public WhatsAppMessageWorker(
        WhatsAppMessageQueue queue, IServiceScopeFactory scopeFactory, ILogger<WhatsAppMessageWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Uma mensagem por vez, na ordem em que chegaram, para não embaralhar a conversa de um usuário.
        await foreach (var message in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<WhatsAppMessageHandler>();
                await handler.HandleAsync(message, stoppingToken);
                _logger.LogInformation(
                    "Mensagem {MessageId} processada e respondida em {Elapsed} ms.", message.MessageId, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao processar a mensagem {MessageId} do WhatsApp.", message.MessageId);
            }
        }
    }
}
