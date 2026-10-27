using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Conversation;
using Nexo.Application.Goals;
using Nexo.Application.Projections;
using Nexo.Domain.Entities;

namespace Nexo.Tests.Application;

public class SavingsGoalTests
{
    private readonly User _user = new("5511944443333");
    private readonly Mock<ISavingsGoalRepository> _repository = new();
    private readonly Mock<IPurchaseProjectionService> _projection = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    // Último dia do mês daqui a 6 meses: dá exatamente 6 meses de prazo.
    private static DateTime InSixMonths()
    {
        var target = DateTime.UtcNow.AddMonths(6);
        return new DateTime(target.Year, target.Month, DateTime.DaysInMonth(target.Year, target.Month));
    }

    private static PurchaseProjection ProjectionWithMargin(decimal margin) =>
        new(1m, 0m, 0m, 0m, margin, false, null, null, 1, 1m, false);

    private SavingsGoalIntentHandler CreateHandler(decimal margin = 600m, params SavingsGoal[] existing)
    {
        _repository.Setup(r => r.GetActiveByUserIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _projection.Setup(p => p.ProjectAsync(_user.Id, It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectionWithMargin(margin));
        return new SavingsGoalIntentHandler(_repository.Object, _projection.Object, _unitOfWork.Object);
    }

    private SavingsGoal Goal(string name, decimal target, decimal saved = 0m, DateTime? deadline = null)
    {
        var goal = new SavingsGoal(_user.Id, name, target, deadline);
        if (saved > 0) goal.AddSaved(saved);
        return goal;
    }

    [Fact]
    public async Task Create_WithDeadline_SavesAndShowsTheMonthlyAmountToSave()
    {
        var handler = CreateHandler(margin: 600m);

        var result = await handler.HandleAsync(_user,
            new MessageIntent(IntentType.CreateSavingsGoal, Amount: 3000m, Description: "viagem", TargetDate: InSixMonths()));

        Assert.True(result.Understood);
        Assert.Contains("Meta criada", result.ReplyMessage);
        Assert.Contains("viagem", result.ReplyMessage);
        Assert.Contains("6 meses", result.ReplyMessage);
        Assert.Contains("R$ 500,00 por mês", result.ReplyMessage);
        Assert.Contains("dá para chegar no prazo", result.ReplyMessage);
        _repository.Verify(r => r.AddAsync(
            It.Is<SavingsGoal>(g => g.TargetAmount == 3000m && g.Name == "viagem"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenTheMarginIsTooSmall_WarnsItWillBeTight()
    {
        var result = await CreateHandler(margin: 300m).HandleAsync(_user,
            new MessageIntent(IntentType.CreateSavingsGoal, Amount: 3000m, TargetDate: InSixMonths()));

        Assert.Contains("apertado", result.ReplyMessage);
        Assert.Contains("Minha meta", result.ReplyMessage);
    }

    [Fact]
    public async Task Create_WhenTheMarginIsNegative_SuggestsReviewingExpenses()
    {
        var result = await CreateHandler(margin: -50m).HandleAsync(_user,
            new MessageIntent(IntentType.CreateSavingsGoal, Amount: 3000m, TargetDate: InSixMonths()));

        Assert.Contains("rever os gastos", result.ReplyMessage);
    }

    [Fact]
    public async Task Create_WithoutDeadline_EstimatesHowLongItTakes()
    {
        var result = await CreateHandler(margin: 500m).HandleAsync(_user,
            new MessageIntent(IntentType.CreateSavingsGoal, Amount: 3000m, Description: "carro"));

        Assert.Contains("Faltam R$ 3.000,00", result.ReplyMessage);
        Assert.Contains("cerca de 6 meses", result.ReplyMessage);
    }

    [Fact]
    public async Task Create_WithoutAnAmount_AsksForIt()
    {
        var result = await CreateHandler().HandleAsync(_user, new MessageIntent(IntentType.CreateSavingsGoal));

        Assert.False(result.Understood);
        Assert.Contains("Qual valor", result.ReplyMessage);
        _repository.Verify(r => r.AddAsync(It.IsAny<SavingsGoal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithADeadlineInThePast_AsksForAnotherDate()
    {
        var result = await CreateHandler().HandleAsync(_user,
            new MessageIntent(IntentType.CreateSavingsGoal, Amount: 1000m, TargetDate: DateTime.UtcNow.AddDays(-30)));

        Assert.False(result.Understood);
        Assert.Contains("já passou", result.ReplyMessage);
    }

    [Fact]
    public async Task Create_WithTheNameOfAnExistingGoal_UpdatesItInsteadOfDuplicating()
    {
        var existing = Goal("Viagem", 1000m, saved: 200m);
        var handler = CreateHandler(600m, existing);

        var result = await handler.HandleAsync(_user,
            new MessageIntent(IntentType.CreateSavingsGoal, Amount: 4000m, Description: "viagem"));

        Assert.Contains("Meta atualizada", result.ReplyMessage);
        Assert.Equal(4000m, existing.TargetAmount);
        Assert.Equal(200m, existing.SavedAmount);
        _repository.Verify(r => r.AddAsync(It.IsAny<SavingsGoal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deposit_ByName_AddsToTheRightGoal()
    {
        var trip = Goal("Viagem", 3000m);
        var car = Goal("Carro", 20000m);
        var handler = CreateHandler(600m, trip, car);

        var result = await handler.HandleAsync(_user,
            new MessageIntent(IntentType.AddToSavingsGoal, Amount: 200m, Description: "viagem"));

        Assert.True(result.Understood);
        Assert.Equal(200m, trip.SavedAmount);
        Assert.Equal(0m, car.SavedAmount);
        Assert.Contains("Guardado", result.ReplyMessage);
        Assert.Contains("7%", result.ReplyMessage);
    }

    [Fact]
    public async Task Deposit_WithoutAName_UsesTheOnlyGoal()
    {
        var trip = Goal("Viagem", 1000m);

        var result = await CreateHandler(600m, trip).HandleAsync(_user,
            new MessageIntent(IntentType.AddToSavingsGoal, Amount: 100m));

        Assert.True(result.Understood);
        Assert.Equal(100m, trip.SavedAmount);
    }

    [Fact]
    public async Task Deposit_WithoutANameAndSeveralGoals_AsksWhich()
    {
        var trip = Goal("Viagem", 1000m);
        var car = Goal("Carro", 2000m);

        var result = await CreateHandler(600m, trip, car).HandleAsync(_user,
            new MessageIntent(IntentType.AddToSavingsGoal, Amount: 100m));

        Assert.False(result.Understood);
        Assert.Contains("Viagem", result.ReplyMessage);
        Assert.Contains("Carro", result.ReplyMessage);
        Assert.Equal(0m, trip.SavedAmount + car.SavedAmount);
    }

    [Fact]
    public async Task Deposit_ThatCompletesTheGoal_Celebrates()
    {
        var trip = Goal("Viagem", 1000m, saved: 900m);

        var result = await CreateHandler(600m, trip).HandleAsync(_user,
            new MessageIntent(IntentType.AddToSavingsGoal, Amount: 150m));

        Assert.True(trip.IsReached);
        Assert.Contains("Meta batida", result.ReplyMessage);
    }

    [Fact]
    public async Task Deposit_WithoutAnyGoal_ExplainsHowToCreateOne()
    {
        var result = await CreateHandler().HandleAsync(_user, new MessageIntent(IntentType.AddToSavingsGoal, Amount: 100m));

        Assert.False(result.Understood);
        Assert.Contains("quero juntar", result.ReplyMessage);
    }

    [Fact]
    public async Task Deposit_WithoutAnAmount_AsksForIt()
    {
        var result = await CreateHandler(600m, Goal("Viagem", 1000m)).HandleAsync(_user,
            new MessageIntent(IntentType.AddToSavingsGoal));

        Assert.False(result.Understood);
        Assert.Contains("Quanto", result.ReplyMessage);
    }

    [Fact]
    public async Task List_ShowsEveryGoalWithProgress()
    {
        var result = await CreateHandler(600m, Goal("Viagem", 1000m, saved: 500m, deadline: InSixMonths()), Goal("Carro", 2000m))
            .HandleAsync(_user, new MessageIntent(IntentType.QuerySavingsGoals));

        Assert.Contains("Suas metas", result.ReplyMessage);
        Assert.Contains("Viagem", result.ReplyMessage);
        Assert.Contains("Carro", result.ReplyMessage);
        Assert.Contains("50%", result.ReplyMessage);
    }

    [Fact]
    public async Task List_WithoutGoals_ExplainsHowToCreateOne()
    {
        var result = await CreateHandler().HandleAsync(_user, new MessageIntent(IntentType.QuerySavingsGoals));

        Assert.True(result.Understood);
        Assert.Contains("quero juntar", result.ReplyMessage);
    }

    [Fact]
    public async Task List_OfAnExpiredGoal_SaysTheDeadlinePassed()
    {
        var expired = Goal("Viagem", 1000m, saved: 100m, deadline: DateTime.UtcNow.AddDays(-20));

        var result = await CreateHandler(600m, expired).HandleAsync(_user, new MessageIntent(IntentType.QuerySavingsGoals));

        Assert.Contains("já passou", result.ReplyMessage);
        Assert.Contains("R$ 900,00", result.ReplyMessage);
    }
}
