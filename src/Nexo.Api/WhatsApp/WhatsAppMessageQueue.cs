using System.Threading.Channels;
using Nexo.Application.WhatsApp;

namespace Nexo.Api.WhatsApp;

// A Meta espera um 200 em poucos segundos, e interpretar a mensagem com a IA pode demorar mais.
// Por isso o webhook só enfileira a mensagem e responde 200; o processamento acontece em segundo plano.
public class WhatsAppMessageQueue
{
    private readonly Channel<WhatsAppInboundMessage> _channel = Channel.CreateUnbounded<WhatsAppInboundMessage>();

    public ValueTask EnqueueAsync(WhatsAppInboundMessage message, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<WhatsAppInboundMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
