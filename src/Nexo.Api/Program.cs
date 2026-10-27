using Microsoft.EntityFrameworkCore;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Api.Workers;
using Nexo.Application.Charts;
using Nexo.Application.Goals;
using Nexo.Application.Recurring;
using Nexo.Application.Reports;
using Nexo.Application.Search;
using Nexo.Infrastructure.Charts;
using Nexo.Api.WhatsApp;
using Nexo.Application.Conversation;
using Nexo.Application.WhatsApp;
using Nexo.Infrastructure.WhatsApp;
using Nexo.Application.Projections;
using Nexo.Application.Parsing;
using Nexo.Application.Transactions;
using Nexo.Infrastructure.Parsing;
using Nexo.Infrastructure.Persistence;
using Nexo.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<NexoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddHttpClient<ITransactionMessageParser, GeminiTransactionMessageParser>();
builder.Services.AddHttpClient<IMessageIntentParser, GeminiIntentParser>();

builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.AddHttpClient<IWhatsAppMessenger, WhatsAppCloudMessenger>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<WhatsAppMessageQueue>();
builder.Services.AddHostedService<WhatsAppMessageWorker>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IInstallmentPlanRepository, InstallmentPlanRepository>();
builder.Services.AddScoped<IBudgetLimitRepository, BudgetLimitRepository>();
builder.Services.AddScoped<IBudgetAlertService, BudgetAlertService>();
builder.Services.AddScoped<IPurchaseProjectionService, PurchaseProjectionService>();
builder.Services.AddScoped<ISavingsGoalRepository, SavingsGoalRepository>();
builder.Services.AddScoped<IIntentHandler, SavingsGoalIntentHandler>();
builder.Services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
builder.Services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
builder.Services.AddScoped<IIntentHandler, RecurringIntentHandler>();
builder.Services.AddHostedService<RecurringTransactionWorker>();
builder.Services.AddHttpClient<IReplyComposer, GeminiReplyComposer>();
builder.Services.AddScoped<ITransactionSearchService, TransactionSearchService>();
builder.Services.AddScoped<IIntentHandler, TransactionSearchIntentHandler>();
builder.Services.AddScoped<IMonthPaceService, MonthPaceService>();
builder.Services.AddScoped<IIntentHandler, MonthPaceIntentHandler>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IChartService, ChartService>();
builder.Services.AddSingleton<IChartRenderer, SkiaChartRenderer>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IConversationHistoryRepository, ConversationHistoryRepository>();
builder.Services.AddScoped<MessageProcessingService>();
builder.Services.AddScoped<ITransactionRegistrar>(sp => sp.GetRequiredService<MessageProcessingService>());
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<WhatsAppMessageHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "NEXO API",
        Version = "v1",
        Description = "Assistente financeiro inteligente - API de backend"
    });
});

var app = builder.Build();

// A primeira imagem desenhada carrega a biblioteca gráfica (cerca de 2 s). Desenhar uma vez na subida
// evita essa espera na primeira resposta real.
try
{
    app.Services.GetRequiredService<IChartRenderer>().RenderMonthlyChart("", new[] { new MonthlyBar("", 1m, 1m) });
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Não foi possível aquecer o gerador de gráficos.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NEXO API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/privacy", () => Results.Content(Nexo.Api.PrivacyPolicyPage.Html, "text/html; charset=utf-8"))
    .ExcludeFromDescription();

app.Run();
