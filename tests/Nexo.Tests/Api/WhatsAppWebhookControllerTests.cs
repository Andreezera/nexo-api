using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nexo.Api.Contracts;
using Nexo.Api.Controllers;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Charts;
using Nexo.Application.Conversation;
using Nexo.Application.Projections;
using Nexo.Application.Reports;
using Nexo.Application.Transactions;
using Nexo.Domain.Entities;

namespace Nexo.Tests.Api;

public class WhatsAppWebhookControllerTests
{
    private const string Phone = "5511999999999";

    private readonly Mock<IMessageIntentParser> _intentParser = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IConversationHistoryRepository> _historyRepository = new();

    public WhatsAppWebhookControllerTests()
    {
        var user = new User(Phone);
        _userRepository.Setup(u => u.GetByPhoneNumberAsync(Phone, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _historyRepository.Setup(h => h.GetRecentAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConversationMessage>());
        _intentParser.Setup(p => p.ParseAsync(It.IsAny<MessageContent>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageIntent(IntentType.Unknown, Reply: "ok"));
    }

    private WhatsAppWebhookController CreateController()
    {
        var service = new ConversationService(
            _intentParser.Object, Mock.Of<ITransactionRegistrar>(), _userRepository.Object,
            Mock.Of<ITransactionRepository>(), Mock.Of<IBudgetLimitRepository>(), Mock.Of<IBudgetAlertService>(),
            Mock.Of<IPurchaseProjectionService>(), Mock.Of<IReportService>(), Mock.Of<IChartService>(),
            _historyRepository.Object, Mock.Of<IUnitOfWork>());
        return new WhatsAppWebhookController(service);
    }

    private static MediaMessageForm Form(byte[] bytes, string contentType, string? caption = null) => new()
    {
        From = Phone,
        Caption = caption,
        File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "arquivo")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        }
    };

    [Fact]
    public async Task ReceiveMedia_WithAudio_SendsBytesAndNormalizedMimeTypeToTheParser()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };

        var result = await CreateController().ReceiveMedia(Form(bytes, "audio/ogg; codecs=opus"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _intentParser.Verify(p => p.ParseAsync(
            It.Is<MessageContent>(c => c.HasMedia && c.MediaMimeType == "audio/ogg" && c.MediaData!.SequenceEqual(bytes)),
            It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveMedia_WithImageAndCaption_KeepsTheCaptionAsText()
    {
        await CreateController().ReceiveMedia(Form(new byte[] { 9 }, "image/jpeg", "nota do mercado"), CancellationToken.None);

        _intentParser.Verify(p => p.ParseAsync(
            It.Is<MessageContent>(c => c.Text == "nota do mercado" && c.MediaMimeType == "image/jpeg"),
            It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveMedia_WithUnsupportedFileType_ReturnsBadRequest()
    {
        var result = await CreateController().ReceiveMedia(Form(new byte[] { 1 }, "application/pdf"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _intentParser.Verify(p => p.ParseAsync(
            It.IsAny<MessageContent>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveMedia_WithEmptyFile_ReturnsBadRequest()
    {
        var result = await CreateController().ReceiveMedia(Form(Array.Empty<byte>(), "audio/ogg"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReceiveMessage_WithText_PassesTextToTheParser()
    {
        var message = new IncomingWhatsAppMessage(Phone, new WhatsAppText("gastei 30 no mercado"));

        var result = await CreateController().ReceiveMessage(message, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        _intentParser.Verify(p => p.ParseAsync(
            It.Is<MessageContent>(c => c.Text == "gastei 30 no mercado" && !c.HasMedia),
            It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
