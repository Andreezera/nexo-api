using Microsoft.EntityFrameworkCore;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Conversation;
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

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IInstallmentPlanRepository, InstallmentPlanRepository>();
builder.Services.AddScoped<IBudgetLimitRepository, BudgetLimitRepository>();
builder.Services.AddScoped<IBudgetAlertService, BudgetAlertService>();
builder.Services.AddScoped<IPurchaseProjectionService, PurchaseProjectionService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IConversationHistoryRepository, ConversationHistoryRepository>();
builder.Services.AddScoped<MessageProcessingService>();
builder.Services.AddScoped<ITransactionRegistrar>(sp => sp.GetRequiredService<MessageProcessingService>());
builder.Services.AddScoped<ConversationService>();

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

app.Run();
