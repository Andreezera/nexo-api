using Nexo.Application.Parsing;

namespace Nexo.Application.Transactions;

public interface ITransactionRegistrar
{
    Task<RegisterMessageResult> RegisterAsync(
        string phoneNumber, ParsedTransaction parsed, CancellationToken cancellationToken = default);
}
