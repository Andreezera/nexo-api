using Nexo.Application.Parsing;
using Nexo.Domain.Enums;

namespace Nexo.Application.Conversation;

public enum IntentType
{
    Unknown = 0,
    RegisterTransaction,
    QueryBalance,
    QuerySpending,
    QueryRecentTransactions,
    QueryPurchaseProjection,
    QueryBudgets,
    SetBudgetLimit
}

// MonthOffset: 0 = mês atual, -1 = mês passado, e assim por diante (só vale para QuerySpending).
// Reply: resposta pronta da IA para mensagens que não são nenhuma das intenções acima.
public record MessageIntent(
    IntentType Type,
    ParsedTransaction? Transaction = null,
    TransactionCategory? Category = null,
    decimal? Amount = null,
    int? InstallmentCount = null,
    int MonthOffset = 0,
    int? RecentCount = null,
    string? Reply = null);
