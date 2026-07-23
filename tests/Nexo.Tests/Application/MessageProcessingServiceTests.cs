using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Parsing;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;
using Nexo.Domain.Enums;
using Xunit;

namespace Nexo.Tests.Application;

public class MessageProcessingServiceTests
{
    private readonly Mock<ITransactionMessageParser> _parser = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();
    private readonly Mock<IInstallmentPlanRepository> _installmentPlanRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private MessageProcessingService CreateService() => new(
        _parser.Object, _userRepository.Object, _transactionRepository.Object,
        _installmentPlanRepository.Object, _unitOfWork.Object);

    [Fact]
    public async Task ProcessAsync_WhenParserDoesNotRecognizeMessage_ReturnsNotUnderstoodAndTouchesNoRepository()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParsedTransaction?)null);

        var result = await CreateService().ProcessAsync("5511999999999", "oi tudo bem?");

        Assert.False(result.Understood);
        _userRepository.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(t => t.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenPhoneNumberIsUnknown_CreatesNewUser()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedTransaction(TransactionType.Expense, 30m, "Mercado", "Mercado", DateTime.UtcNow, null));
        _userRepository.Setup(u => u.GetByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await CreateService().ProcessAsync("5511999999999", "gastei 30 reais no mercado");

        _userRepository.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenPhoneNumberIsAlreadyKnown_DoesNotCreateNewUser()
    {
        var existingUser = new User("5511999999999");
        _parser.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedTransaction(TransactionType.Expense, 30m, "Mercado", "Mercado", DateTime.UtcNow, null));
        _userRepository.Setup(u => u.GetByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        await CreateService().ProcessAsync("5511999999999", "gastei 30 reais no mercado");

        _userRepository.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenMessageHasNoInstallments_SavesSingleTransactionOnly()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedTransaction(TransactionType.Expense, 30m, "Mercado", "Mercado", DateTime.UtcNow, null));
        _userRepository.Setup(u => u.GetByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User("5511999999999"));

        var result = await CreateService().ProcessAsync("5511999999999", "gastei 30 reais no mercado");

        Assert.True(result.Understood);
        _transactionRepository.Verify(t => t.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _installmentPlanRepository.Verify(p => p.AddAsync(It.IsAny<InstallmentPlan>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenMessageHasInstallments_SavesInstallmentPlanInsteadOfSingleTransaction()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedTransaction(TransactionType.Expense, 3000m, "Eletrônicos", "Notebook", DateTime.UtcNow, 10));
        _userRepository.Setup(u => u.GetByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User("5511999999999"));

        var result = await CreateService().ProcessAsync("5511999999999", "comprei um notebook de 3000 em 10x");

        Assert.True(result.Understood);
        Assert.Contains("10x", result.ReplyMessage);
        _installmentPlanRepository.Verify(p => p.AddAsync(It.IsAny<InstallmentPlan>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionRepository.Verify(t => t.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
