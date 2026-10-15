using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Messaging;
using Nexo.Application.Parsing;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Application;

// Detalhes de apresentação das respostas, descobertos nos testes reais pelo WhatsApp.
public class ReplyPolishTests
{
    [Theory]
    [InlineData(0.3, "🟩")]
    [InlineData(0.7, "🟨")]
    [InlineData(1.0, "🟥")]
    public void StatusSquare_FollowsTheSameThresholdsAsTheDot(double fraction, string expected)
    {
        Assert.Equal(expected, MessageFormatter.StatusSquare((decimal)fraction));
    }

    [Fact]
    public void Bar_CanUseAnotherFilledSquare()
    {
        Assert.Equal("🟥🟥🟥🟥🟥⬜⬜⬜⬜⬜", MessageFormatter.Bar(0.5m, 10, "🟥"));
    }

    private static async Task<string> RegisterAsync(string description, TransactionCategory category)
    {
        var user = new User("5511900000000");
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByPhoneNumberAsync(user.PhoneNumber, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var alerts = new Mock<IBudgetAlertService>();

        var service = new MessageProcessingService(
            Mock.Of<ITransactionMessageParser>(), users.Object, Mock.Of<ITransactionRepository>(),
            Mock.Of<IInstallmentPlanRepository>(), Mock.Of<IUnitOfWork>(), alerts.Object);

        var result = await service.RegisterAsync(
            user.PhoneNumber,
            new ParsedTransaction(TransactionType.Expense, 30m, category, description, new DateTime(2026, 11, 25), null));
        return result.ReplyMessage;
    }

    [Fact]
    public async Task Register_HidesTheDescriptionLineWhenItOnlyRepeatsTheCategory()
    {
        var reply = await RegisterAsync("Mercado", TransactionCategory.Mercado);

        Assert.DoesNotContain("📝", reply);
        Assert.Contains("R$ 30,00", reply);
        Assert.Contains("25/11", reply);
    }

    [Fact]
    public async Task Register_ShowsTheDescriptionWhenItAddsInformation()
    {
        var reply = await RegisterAsync("Padaria", TransactionCategory.Alimentacao);

        Assert.Contains("📝 Padaria", reply);
    }
}
