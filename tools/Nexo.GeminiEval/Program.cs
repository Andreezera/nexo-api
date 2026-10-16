using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.Conversation;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Parsing;

// Avaliação manual e comparativa de modelos de IA. Roda o mesmo conjunto de mensagens, com resultado esperado
// conhecido, em vários modelos do Gemini e compara assertividade e tempo de resposta. Não faz parte da suíte
// automática porque depende de chamadas reais à API (custo, latência, não-determinismo). O resultado é gravado
// em docs/avaliacao-modelos.md.
//
// Uso: dotnet run --project tools/Nexo.GeminiEval [modelo1 modelo2 ...]

const string ApiUserSecretsId = "203d2fc0-3c53-4570-91e5-012efd1780f6";

var defaultModels = new[]
{
    "gemini-3.1-flash-lite", "gemini-3.5-flash-lite", "gemini-3.5-flash",
    "gemini-3.8-flash", "gemini-3.1-pro-preview", "gemini-pro-latest"
};
// --sem-relatorio roda a avaliação sem sobrescrever docs/avaliacao-modelos.md (útil para testar um modelo ou prompt novo).
var writeReport = !args.Contains("--sem-relatorio");
var modelArgs = args.Where(a => !a.StartsWith("--")).ToArray();
var models = modelArgs.Length > 0 ? modelArgs : defaultModels;

var configuration = new ConfigurationBuilder().AddUserSecrets(ApiUserSecretsId).Build();
var apiKey = configuration["Gemini:ApiKey"]
    ?? throw new InvalidOperationException("Gemini:ApiKey não configurado em user-secrets.");

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Error));

var cases = BuildCases();
Console.WriteLine($"{cases.Count} casos, {models.Length} modelos: {string.Join(", ", models)}");

// Os modelos rodam em paralelo; dentro de cada modelo os casos rodam em sequência.
var results = (await Task.WhenAll(models.Select(RunModelAsync))).ToList();

PrintSummary(results, cases.Count);
if (writeReport)
{
    var path = WriteMarkdown(results, cases);
    Console.WriteLine($"\nRelatório gravado em {path}");
}
else
{
    foreach (var failed in results.SelectMany(r => r.Cases.Where(c => !c.Ok).Select(c => (r.Model, c))))
        Console.WriteLine($"FALHA [{failed.Model}] \"{failed.c.Case.Message}\": {failed.c.Detail}");
}

// ---------------------------------------------------------------------------------------------

async Task<ModelResult> RunModelAsync(string model)
{
    var parser = new GeminiIntentParser(
        httpClient,
        Options.Create(new GeminiOptions { ApiKey = apiKey, Model = model }),
        loggerFactory.CreateLogger<GeminiIntentParser>());

    var caseResults = new List<CaseResult>();
    foreach (var testCase in cases)
    {
        MessageIntent? intent = null;
        var elapsed = TimeSpan.Zero;

        // Falha do provedor (sobrecarga, rede) não é erro de classificação: tenta de novo antes de contar como falha.
        for (var attempt = 1; attempt <= 3 && intent is null; attempt++)
        {
            var watch = Stopwatch.StartNew();
            intent = await parser.ParseAsync(MessageContent.FromText(testCase.Message), BuildHistory(testCase));
            watch.Stop();
            elapsed = watch.Elapsed;
            if (intent is null)
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
        }

        var (ok, intentOk, detail) = Evaluate(testCase, intent);
        caseResults.Add(new CaseResult(testCase, intent, elapsed, ProviderFailed: intent is null, ok, intentOk, detail));
        Console.WriteLine($"[{model}] {(ok ? "OK    " : intent is null ? "ERRO  " : "FALHOU")} {testCase.Message}");
    }

    return new ModelResult(model, caseResults);
}

static IReadOnlyList<ConversationMessage> BuildHistory(EvalCase testCase)
{
    if (testCase.Context is null)
        return Array.Empty<ConversationMessage>();

    var user = Guid.NewGuid();
    return new[]
    {
        new ConversationMessage(user, ConversationRole.User, testCase.Context.Value.User),
        new ConversationMessage(user, ConversationRole.Assistant, testCase.Context.Value.Assistant)
    };
}

static (bool Ok, bool IntentOk, string Detail) Evaluate(EvalCase testCase, MessageIntent? intent)
{
    if (intent is null)
        return (false, false, "sem resposta do provedor");

    if (intent.Type != testCase.Intent)
        return (false, false, $"intenção {intent.Type}, esperado {testCase.Intent}");

    var problems = new List<string>();

    if (testCase.Intent is IntentType.RegisterTransaction or IntentType.CreateRecurringTransaction)
    {
        if (intent.Transaction is null) return (false, true, "sem transação");
        var t = intent.Transaction;
        if (testCase.DayOfMonth is { } dayOfMonth && intent.DayOfMonth != dayOfMonth) problems.Add($"dia {intent.DayOfMonth}");
        if (testCase.Type is { } type && t.Type != type) problems.Add($"tipo {t.Type}");
        if (testCase.Amount is { } amount && Math.Abs(t.Amount - amount) > 0.01m) problems.Add($"valor {t.Amount}");
        if (testCase.Category is { } category && t.Category != category) problems.Add($"categoria {t.Category}");
        if (testCase.Installments is { } installments && t.InstallmentCount != installments) problems.Add($"parcelas {t.InstallmentCount}");
        if (testCase.Installments is null && t.InstallmentCount is > 1) problems.Add($"parcelas {t.InstallmentCount}");
        if (testCase.Intent == IntentType.RegisterTransaction && testCase.DayOffset is { } offset && t.OccurredAt.Date != DateTime.UtcNow.Date.AddDays(offset))
            problems.Add($"data {t.OccurredAt:yyyy-MM-dd}");
    }
    else
    {
        if (testCase.Amount is { } amount && (intent.Amount is null || Math.Abs(intent.Amount.Value - amount) > 0.01m)) problems.Add($"valor {intent.Amount}");
        if (testCase.Category is { } category && intent.Category != category) problems.Add($"categoria {intent.Category}");
        if (testCase.CategoryMustBeEmpty && intent.Category is not null) problems.Add($"categoria {intent.Category}");
        if (testCase.Installments is { } installments && intent.InstallmentCount != installments) problems.Add($"parcelas {intent.InstallmentCount}");
        if (testCase.MonthOffset is { } month && intent.MonthOffset != month) problems.Add($"mês {intent.MonthOffset}");
        if (testCase.RecentCount is { } recent && intent.RecentCount != recent) problems.Add($"quantidade {intent.RecentCount}");
        if (testCase.Chart is { } chart && intent.Chart != chart) problems.Add($"gráfico {intent.Chart}");
    }

    return problems.Count == 0 ? (true, true, "") : (false, true, string.Join(", ", problems));
}

static void PrintSummary(List<ModelResult> results, int total)
{
    Console.WriteLine();
    Console.WriteLine($"{"Modelo",-26} {"Acerto",-10} {"Intenção",-10} {"Latência média",-16} {"Falhas do provedor"}");
    Console.WriteLine(new string('-', 80));
    foreach (var r in results.OrderByDescending(r => r.Correct))
        Console.WriteLine($"{r.Model,-26} {r.Correct + "/" + total,-10} {r.IntentCorrect + "/" + total,-10} {r.AverageLatencySeconds.ToString("F1", CultureInfo.InvariantCulture) + " s",-16} {r.ProviderFailures}");
}

string WriteMarkdown(List<ModelResult> results, List<EvalCase> evalCases)
{
    var sb = new StringBuilder();
    var inv = CultureInfo.InvariantCulture;
    sb.AppendLine("# Avaliação comparativa de modelos de IA");
    sb.AppendLine();
    sb.AppendLine($"Executada com `tools/Nexo.GeminiEval` em {DateTime.Now:dd/MM/yyyy}, com o mesmo conjunto de {evalCases.Count} mensagens em cada modelo.");
    sb.AppendLine("O interpretador é o mesmo usado em produção (`GeminiIntentParser`): o modelo classifica a intenção da mensagem e extrai os dados,");
    sb.AppendLine("e um caso só conta como acerto quando a intenção e todos os campos esperados batem. Falha do provedor (sobrecarga, rede) é tentada de novo até 3 vezes e só então contada à parte.");
    sb.AppendLine();
    sb.AppendLine("## Resultado geral");
    sb.AppendLine();
    sb.AppendLine("| Modelo | Acerto total | Acerto da intenção | Latência média | Falhas do provedor |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var r in results.OrderByDescending(r => r.Correct).ThenBy(r => r.AverageLatencySeconds))
        sb.AppendLine($"| `{r.Model}` | {r.Correct}/{evalCases.Count} ({100.0 * r.Correct / evalCases.Count:F1}%) | {r.IntentCorrect}/{evalCases.Count} ({100.0 * r.IntentCorrect / evalCases.Count:F1}%) | {r.AverageLatencySeconds.ToString("F1", inv)} s | {r.ProviderFailures} |");

    sb.AppendLine();
    sb.AppendLine("## Acerto por tipo de mensagem");
    sb.AppendLine();
    var groups = evalCases.Select(c => c.Group).Distinct().ToList();
    sb.AppendLine("| Modelo | " + string.Join(" | ", groups.Select(g => $"{g} ({evalCases.Count(c => c.Group == g)})")) + " |");
    sb.AppendLine("|---|" + string.Join("|", groups.Select(_ => "---")) + "|");
    foreach (var r in results)
        sb.AppendLine($"| `{r.Model}` | " + string.Join(" | ", groups.Select(g => r.Cases.Count(c => c.Case.Group == g && c.Ok))) + " |");

    sb.AppendLine();
    sb.AppendLine("## Erros por modelo");
    foreach (var r in results)
    {
        var failures = r.Cases.Where(c => !c.Ok).ToList();
        sb.AppendLine();
        sb.AppendLine($"### `{r.Model}`");
        sb.AppendLine();
        if (failures.Count == 0)
        {
            sb.AppendLine("Sem erros.");
            continue;
        }
        foreach (var f in failures)
            sb.AppendLine($"- \"{f.Case.Message}\": {f.Detail}");
    }

    sb.AppendLine();
    sb.AppendLine("## Casos avaliados");
    sb.AppendLine();
    foreach (var group in groups)
    {
        sb.AppendLine($"**{group}**");
        sb.AppendLine();
        foreach (var c in evalCases.Where(c => c.Group == group))
            sb.AppendLine($"- \"{c.Message}\"{(c.Context is not null ? " (com contexto de conversa anterior)" : "")}");
        sb.AppendLine();
    }

    var dir = AppContext.BaseDirectory;
    while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
        dir = Directory.GetParent(dir)?.FullName;
    var path = Path.Combine(dir ?? Directory.GetCurrentDirectory(), "docs", "avaliacao-modelos.md");
    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    return path;
}

static List<EvalCase> BuildCases()
{
    const string Reg = "Registro de transação", Qry = "Consulta", Cmd = "Comando", Unk = "Fora do escopo", Ctx = "Depende do contexto";
    const string New = "Menu, resumo, gráfico e correção";
    var balanceContext = ("quanto gastei com mercado esse mês?", "Em novembro de 2026, você gastou R$ 90,00 em Mercado.");

    return new List<EvalCase>
    {
        // registro de transação
        new(Reg, "gastei 30 reais no mercado", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 30m, Category: TransactionCategory.Mercado),
        new(Reg, "ganhei 500 reais hoje", IntentType.RegisterTransaction, Type: TransactionType.Income, Amount: 500m),
        new(Reg, "comprei um notebook de 3000 reais em 10 parcelas", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 3000m, Category: TransactionCategory.Eletronicos, Installments: 10),
        new(Reg, "paguei 89,90 de internet", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 89.90m),
        new(Reg, "recebi 1200 de freelance", IntentType.RegisterTransaction, Type: TransactionType.Income, Amount: 1200m, Category: TransactionCategory.Freelance),
        new(Reg, "gastei 15 reais de uber", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 15m, Category: TransactionCategory.Transporte),
        new(Reg, "comprei um celular em 12x de 150", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 1800m, Installments: 12), // exige multiplicar 150 x 12
        new(Reg, "gastei 200 reais no cinema com a família", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 200m, Category: TransactionCategory.Lazer),
        new(Reg, "paguei 45 reais de remédio na farmácia", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 45m, Category: TransactionCategory.Saude),
        new(Reg, "comprei uma bicicleta de 900 reais em 6x", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 900m, Installments: 6),
        new(Reg, "recebi reembolso de 120 reais", IntentType.RegisterTransaction, Type: TransactionType.Income, Amount: 120m),
        new(Reg, "ontem gastei 60 no almoço", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 60m, Category: TransactionCategory.Alimentacao, DayOffset: -1),
        new(Reg, "anteontem paguei 120 de luz", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 120m, DayOffset: -2),
        new(Reg, "gastei R$ 1.250,50 no aluguel", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 1250.50m, Category: TransactionCategory.Moradia),
        new(Reg, "paguei a assinatura do netflix, 55,90", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 55.90m, Category: TransactionCategory.Assinaturas),
        new(Reg, "dei um presente de 150 pro meu pai", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 150m, Category: TransactionCategory.Presente),
        new(Reg, "pix de 35 reais pro lanche", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 35m, Category: TransactionCategory.Alimentacao),
        new(Reg, "comprei uma camiseta por 80 reais", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 80m, Category: TransactionCategory.Vestuario),

        // consultas sobre os dados
        new(Qry, "qual meu saldo?", IntentType.QueryBalance),
        new(Qry, "quanto eu tenho na conta?", IntentType.QueryBalance),
        new(Qry, "quanto gastei esse mês?", IntentType.QuerySpending, CategoryMustBeEmpty: true, MonthOffset: 0),
        new(Qry, "quanto gastei com mercado esse mês?", IntentType.QuerySpending, Category: TransactionCategory.Mercado, MonthOffset: 0),
        new(Qry, "quanto gastei com transporte no mês passado?", IntentType.QuerySpending, Category: TransactionCategory.Transporte, MonthOffset: -1),
        new(Qry, "me mostra meus últimos 3 lançamentos", IntentType.QueryRecentTransactions, RecentCount: 3),
        new(Qry, "o que eu comprei por último?", IntentType.QueryRecentTransactions),
        new(Qry, "consigo comprar um notebook de 4000?", IntentType.QueryPurchaseProjection, Amount: 4000m),
        new(Qry, "dá pra comprar uma tv de 2400 em 12x?", IntentType.QueryPurchaseProjection, Amount: 2400m, Installments: 12),
        new(Qry, "como estão meus limites?", IntentType.QueryBudgets),

        // comandos
        new(Cmd, "define um limite de 500 pra mercado", IntentType.SetBudgetLimit, Category: TransactionCategory.Mercado, Amount: 500m),
        new(Cmd, "quero gastar no máximo 300 por mês com lazer", IntentType.SetBudgetLimit, Category: TransactionCategory.Lazer, Amount: 300m),
        new(Cmd, "muda o limite de transporte pra 250", IntentType.SetBudgetLimit, Category: TransactionCategory.Transporte, Amount: 250m),

        // fora do escopo
        new(Unk, "quem descobriu o brasil?", IntentType.Unknown),
        new(Unk, "me conta uma piada", IntentType.Unknown),
        new(Unk, "gastei no mercado", IntentType.Unknown), // sem valor: não pode inventar

        // menu, resumo, gráfico e correção do último lançamento
        new(New, "menu", IntentType.ShowMenu),
        new(New, "oi tudo bem?", IntentType.ShowMenu),
        new(New, "bom dia", IntentType.ShowMenu),
        new(New, "o que você faz?", IntentType.ShowMenu),
        new(New, "resumo do mês", IntentType.QueryMonthlySummary, MonthOffset: 0),
        new(New, "resumo de outubro", IntentType.QueryMonthlySummary, MonthOffset: -1),
        new(New, "como foi o mês passado?", IntentType.QueryMonthlySummary, MonthOffset: -1),
        new(New, "me manda um gráfico dos meus gastos", IntentType.QueryChart, Chart: ChartKind.Categories),
        new(New, "gráfico por categoria do mês passado", IntentType.QueryChart, Chart: ChartKind.Categories, MonthOffset: -1),
        new(New, "quero ver um gráfico dos últimos meses", IntentType.QueryChart, Chart: ChartKind.Monthly),
        new(New, "apaga o último", IntentType.UndoLastTransaction),
        new(New, "desfaz isso", IntentType.UndoLastTransaction),
        new(New, "cancela o último lançamento", IntentType.UndoLastTransaction),
        new(New, "corrige para 25", IntentType.EditLastTransaction, Amount: 25m),
        new(New, "o último foi 40 reais", IntentType.EditLastTransaction, Amount: 40m),
        new(New, "muda a categoria do último para lazer", IntentType.EditLastTransaction, Category: TransactionCategory.Lazer),

        // contas recorrentes
        new(New, "netflix 55,90 todo dia 10", IntentType.CreateRecurringTransaction, Type: TransactionType.Expense, Amount: 55.90m, Category: TransactionCategory.Assinaturas, DayOfMonth: 10),
        new(New, "aluguel de 1200 todo dia 5", IntentType.CreateRecurringTransaction, Type: TransactionType.Expense, Amount: 1200m, Category: TransactionCategory.Moradia, DayOfMonth: 5),
        new(New, "meu salário de 3000 cai todo dia 5", IntentType.CreateRecurringTransaction, Type: TransactionType.Income, Amount: 3000m, DayOfMonth: 5),
        new(New, "quais são minhas contas fixas?", IntentType.QueryRecurringTransactions),
        new(New, "cancela a netflix", IntentType.CancelRecurringTransaction),

        // depende da conversa anterior
        new(Ctx, "e no mês passado?", IntentType.QuerySpending, Category: TransactionCategory.Mercado, MonthOffset: -1, Context: balanceContext),
        new(Ctx, "e com lazer?", IntentType.QuerySpending, Category: TransactionCategory.Lazer, MonthOffset: 0, Context: balanceContext),
        new(Ctx, "e mais 20 de estacionamento", IntentType.RegisterTransaction, Type: TransactionType.Expense, Amount: 20m,
            Context: ("gastei 50 de uber", "Despesa registrada: R$ 50,00 em Transporte.")),
        // assunto novo depois de falar de limites: a conversa anterior não pode atrapalhar
        new(Ctx, "gastos por categoria", IntentType.QuerySpending, CategoryMustBeEmpty: true,
            Context: ("como estão meus limites?", "Você ainda não definiu limites de gasto.")),
    };
}

record EvalCase(
    string Group,
    string Message,
    IntentType Intent,
    TransactionType? Type = null,
    decimal? Amount = null,
    TransactionCategory? Category = null,
    bool CategoryMustBeEmpty = false,
    int? Installments = null,
    int? MonthOffset = null,
    int? RecentCount = null,
    int? DayOffset = null,
    ChartKind? Chart = null,
    int? DayOfMonth = null,
    (string User, string Assistant)? Context = null);

record CaseResult(EvalCase Case, MessageIntent? Intent, TimeSpan Elapsed, bool ProviderFailed, bool Ok, bool IntentOk, string Detail);

record ModelResult(string Model, List<CaseResult> Cases)
{
    public int Correct => Cases.Count(c => c.Ok);
    public int IntentCorrect => Cases.Count(c => c.IntentOk);
    public int ProviderFailures => Cases.Count(c => c.ProviderFailed);
    public double AverageLatencySeconds => Cases.Where(c => !c.ProviderFailed).Select(c => c.Elapsed.TotalSeconds).DefaultIfEmpty(0).Average();
}
