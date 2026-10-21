namespace Nexo.Application.Conversation;

// Escreve a resposta em tom natural a partir de fatos que o código já calculou. A IA só dá a forma ao texto,
// os números vêm sempre do banco. Retorna null se não conseguir, e quem chama usa um texto de reserva.
public interface IReplyComposer
{
    Task<string?> ComposeAsync(string question, string facts, CancellationToken cancellationToken = default);
}
