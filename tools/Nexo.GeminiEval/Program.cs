using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Parsing;

// Ferramenta de avaliação manual: mede a taxa de acerto do Gemini na extração
// estruturada de transações, contra um conjunto de mensagens com resultado
// esperado conhecido. Não faz parte da suíte automática de testes porque
// depende de uma chamada de rede real à API do Gemini (custo/latência/
// não-determinismo) — é executada sob demanda para gerar evidência
// documentada no relatório do projeto.

const string ApiUserSecretsId = "203d2fc0-3c53-4570-91e5-012efd1780f6";

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(ApiUserSecretsId)
    .Build();

var apiKey = configuration["Gemini:ApiKey"]
    ?? throw new InvalidOperationException("Gemini:ApiKey não configurado em user-secrets.");

var options = Options.Create(new GeminiOptions { ApiKey = apiKey, Model = "gemini-flash-latest" });
using var httpClient = new HttpClient();
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
var logger = loggerFactory.CreateLogger<GeminiTransactionMessageParser>();
var parser = new GeminiTransactionMessageParser(httpClient, options, logger);

var cases = new List<TestCase>
{
    new("gastei 30 reais no mercado", true, TransactionType.Expense, 30m, null),
    new("ganhei 500 reais hoje", true, TransactionType.Income, 500m, null),
    new("comprei um notebook de 3000 reais em 10 parcelas", true, TransactionType.Expense, 3000m, 10),
    new("paguei 89,90 de internet", true, TransactionType.Expense, 89.90m, null),
    new("recebi 1200 de freelance", true, TransactionType.Income, 1200m, null),
    new("gastei 15 reais de uber", true, TransactionType.Expense, 15m, null),
    new("comprei um celular em 12x de 150", true, TransactionType.Expense, 1800m, 12), // exige o modelo multiplicar 150 x 12
    new("oi tudo bem?", false, null, null, null),
    new("quanto posso gastar esse mês?", false, null, null, null),
    new("recebi 50 reais de aniversário", true, TransactionType.Income, 50m, null),
    new("gastei 200 reais no cinema com a familia", true, TransactionType.Expense, 200m, null),
    new("paguei 45 reais de remedio na farmacia", true, TransactionType.Expense, 45m, null),
    new("comprei uma bicicleta de 900 reais em 6x", true, TransactionType.Expense, 900m, 6),
    new("bom dia", false, null, null, null),
    new("recebi reembolso de 120 reais", true, TransactionType.Income, 120m, null),
};

var passed = 0;
Console.WriteLine($"{"Mensagem",-55} {"Esperado",-18} {"Obtido",-18} {"Resultado"}");
Console.WriteLine(new string('-', 100));

foreach (var testCase in cases)
{
    var result = await parser.ParseAsync(testCase.Message);
    var ok = Evaluate(testCase, result);
    if (ok) passed++;

    var esperado = testCase.ExpectFinancial ? $"{testCase.ExpectedType}/{testCase.ExpectedAmount}" : "não-financeiro";
    var obtido = result is null ? "não-financeiro" : $"{result.Type}/{result.Amount}" + (result.InstallmentCount is { } c ? $"/{c}x" : "");

    Console.WriteLine($"{testCase.Message,-55} {esperado,-18} {obtido,-18} {(ok ? "OK" : "FALHOU")}");
    await Task.Delay(13000); // tier gratuito do gemini-3.6-flash permite 5 requisições/minuto
}

Console.WriteLine(new string('-', 100));
Console.WriteLine($"Acurácia: {passed}/{cases.Count} ({100.0 * passed / cases.Count:F1}%)");

static bool Evaluate(TestCase testCase, Nexo.Application.Parsing.ParsedTransaction? result)
{
    if (!testCase.ExpectFinancial)
        return result is null;

    if (result is null)
        return false;

    return result.Type == testCase.ExpectedType
        && Math.Abs(result.Amount - testCase.ExpectedAmount!.Value) < 0.01m
        && result.InstallmentCount == testCase.ExpectedInstallmentCount;
}

record TestCase(
    string Message,
    bool ExpectFinancial,
    TransactionType? ExpectedType,
    decimal? ExpectedAmount,
    int? ExpectedInstallmentCount);
