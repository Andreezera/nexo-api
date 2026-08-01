using Microsoft.AspNetCore.Mvc;
using Moq;
using Nexo.Api.Contracts;
using Nexo.Api.Controllers;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;

namespace Nexo.Tests.Api;

public class BudgetLimitsControllerTests
{
    private const string Phone = "5511999999999";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IBudgetLimitRepository> _limitRepository = new();
    private readonly Mock<IBudgetAlertService> _alertService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly User _user = new(Phone);

    public BudgetLimitsControllerTests()
    {
        _userRepository.Setup(u => u.GetByPhoneNumberAsync(Phone, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
    }

    private BudgetLimitsController CreateController() =>
        new(_userRepository.Object, _limitRepository.Object, _alertService.Object, _unitOfWork.Object);

    [Fact]
    public async Task Set_WhenLimitDoesNotExist_CreatesIt()
    {
        _limitRepository.Setup(l => l.GetAsync(_user.Id, TransactionCategory.Mercado, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BudgetLimit?)null);
        _alertService.Setup(a => a.GetStatusAsync(_user.Id, TransactionCategory.Mercado, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BudgetStatus(TransactionCategory.Mercado, 800m, 200m, 0.25m));

        var result = await CreateController().Set(Phone, TransactionCategory.Mercado, new SetBudgetLimitRequest(800m), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BudgetLimitResponse>(ok.Value);
        Assert.Equal(25m, response.PercentUsed);
        Assert.False(response.Exceeded);
        _limitRepository.Verify(l => l.AddAsync(It.IsAny<BudgetLimit>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_WhenLimitAlreadyExists_UpdatesInsteadOfCreating()
    {
        var existing = new BudgetLimit(_user.Id, TransactionCategory.Lazer, 300m);
        _limitRepository.Setup(l => l.GetAsync(_user.Id, TransactionCategory.Lazer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _alertService.Setup(a => a.GetStatusAsync(_user.Id, TransactionCategory.Lazer, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BudgetStatus(TransactionCategory.Lazer, 500m, 600m, 1.2m));

        var result = await CreateController().Set(Phone, TransactionCategory.Lazer, new SetBudgetLimitRequest(500m), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<BudgetLimitResponse>(ok.Value).Exceeded);
        Assert.Equal(500m, existing.MonthlyLimit);
        _limitRepository.Verify(l => l.AddAsync(It.IsAny<BudgetLimit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Set_WhenLimitIsNotPositive_ReturnsBadRequest()
    {
        var result = await CreateController().Set(Phone, TransactionCategory.Mercado, new SetBudgetLimitRequest(0m), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Set_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _userRepository.Setup(u => u.GetByPhoneNumberAsync("0000", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateController().Set("0000", TransactionCategory.Mercado, new SetBudgetLimitRequest(100m), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_WhenLimitExists_RemovesIt()
    {
        var existing = new BudgetLimit(_user.Id, TransactionCategory.Lazer, 300m);
        _limitRepository.Setup(l => l.GetAsync(_user.Id, TransactionCategory.Lazer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateController().Delete(Phone, TransactionCategory.Lazer, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _limitRepository.Verify(l => l.Remove(existing), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenLimitDoesNotExist_ReturnsNotFound()
    {
        _limitRepository.Setup(l => l.GetAsync(_user.Id, TransactionCategory.Lazer, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BudgetLimit?)null);

        var result = await CreateController().Delete(Phone, TransactionCategory.Lazer, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsStatusPerLimitAsPercentage()
    {
        _alertService.Setup(a => a.GetAllStatusesAsync(_user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new BudgetStatus(TransactionCategory.Mercado, 1000m, 855m, 0.855m) });

        var result = await CreateController().GetAll(Phone, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<BudgetLimitResponse>>(ok.Value);
        Assert.Single(list);
        Assert.Equal(85.5m, list[0].PercentUsed);
    }
}
