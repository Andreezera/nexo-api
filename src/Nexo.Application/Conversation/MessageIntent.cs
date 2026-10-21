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
    SetBudgetLimit,
    ShowMenu,
    QueryMonthlySummary,
    QueryChart,
    UndoLastTransaction,
    EditLastTransaction,
    CreateRecurringTransaction,
    QueryRecurringTransactions,
    CancelRecurringTransaction,
    QueryMonthPace,
    QueryTransactions
}

public enum SearchAggregation
{
    Sum,
    Count,
    Average,
    Largest,
    List
}

public enum ChartKind
{
    Categories,
    Monthly
}

// MonthOffset: 0 = mês atual, -1 = mês passado, e assim por diante (vale para gastos, resumo e gráfico por categoria).
// Reply: resposta pronta da IA para mensagens que não são nenhuma das intenções acima.
// Chart: qual gráfico foi pedido. Description: novo texto do lançamento (corrigir o último) ou nome da conta
// recorrente a cancelar. DayOfMonth: dia do mês de uma conta recorrente.
// Pergunta livre (QueryTransactions): SearchText é a palavra procurada na descrição, StartDate/EndDate o período
// (nulos valem "todo o histórico até hoje"), Kind o tipo (nulo é gasto e receita) e Aggregation o que o usuário quer saber.
// OriginalText é o texto que o usuário escreveu, para a resposta soar como resposta à pergunta dele.
public record MessageIntent(
    IntentType Type,
    ParsedTransaction? Transaction = null,
    TransactionCategory? Category = null,
    decimal? Amount = null,
    int? InstallmentCount = null,
    int MonthOffset = 0,
    int? RecentCount = null,
    string? Reply = null,
    ChartKind? Chart = null,
    string? Description = null,
    int? DayOfMonth = null,
    string? SearchText = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    TransactionType? Kind = null,
    SearchAggregation? Aggregation = null,
    string? OriginalText = null);
