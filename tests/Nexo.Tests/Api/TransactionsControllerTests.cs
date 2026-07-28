using Microsoft.AspNetCore.Mvc;
using Moq;
using Nexo.Api.Contracts;
using Nexo.Api.Controllers;
using Nexo.Application.Abstractions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using Xunit;

namespace Nexo.Tests.Api;

public class TransactionsControllerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private TransactionsController CreateController() =>
        new(_userRepository.Object, _transactionRepository.Object, _unitOfWork.Object);

    private static Transaction CreateTransaction() => new(
        Guid.NewGuid(), TransactionType.Expense, 100m, TransactionCategory.Mercado, "Compra", new DateTime(2026, 7, 20));

    [Fact]
    public async Task Update_WhenTransactionExists_UpdatesAndReturnsOk()
    {
        var transaction = CreateTransaction();
        _transactionRepository.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        var request = new UpdateTransactionRequest(
            TransactionType.Income, 200m, TransactionCategory.Salario, "Pagamento", new DateTime(2026, 7, 22));

        var result = await CreateController().Update(transaction.Id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TransactionResponse>(okResult.Value);
        Assert.Equal(TransactionType.Income, response.Type);
        Assert.Equal(200m, response.Amount);
        Assert.Equal(TransactionCategory.Salario, response.Category);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenTransactionDoesNotExist_ReturnsNotFound()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);
        var request = new UpdateTransactionRequest(
            TransactionType.Expense, 50m, TransactionCategory.Outros, "Teste", DateTime.UtcNow);

        var result = await CreateController().Update(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenTransactionExists_MarksAsDeletedAndReturnsNoContent()
    {
        var transaction = CreateTransaction();
        _transactionRepository.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var result = await CreateController().Delete(transaction.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(transaction.IsDeleted);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenTransactionDoesNotExist_ReturnsNotFound()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var result = await CreateController().Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
