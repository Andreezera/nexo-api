using Moq;
using Nexo.Application.Abstractions;
using Nexo.Application.Budgets;
using Nexo.Application.Charts;
using Nexo.Application.Conversation;
using Nexo.Application.Projections;
using Nexo.Application.Reports;
using Nexo.Application.Transactions;
using Nexo.Application.WhatsApp;
using Nexo.Domain.Entities;

namespace Nexo.Tests.Application;

public class WhatsAppMessageHandlerTests
{
    private const string Phone = "5511999999999";

    private readonly Mock<IMessageIntentParser> _intentParser = new();
    private readonly Mock<IWhatsAppMessenger> _messenger = new();

    public WhatsAppMessageHandlerTests()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(u => u.GetByPhoneNumberAsync(Phone, It.IsAny<CancellationToken>())).ReturnsAsync(new User(Phone));
        var history = new Mock<IConversationHistoryRepository>();
        history.Setup(h => h.GetRecentAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConversationMessage>());

        _intentParser.Setup(p => p.ParseAsync(It.IsAny<MessageContent>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageIntent(IntentType.Unknown, Reply: "resposta da IA"));

        _conversation = new ConversationService(
            _intentParser.Object, Mock.Of<ITransactionRegistrar>(), userRepository.Object, Mock.Of<ITransactionRepository>(),
            Mock.Of<IBudgetLimitRepository>(), Mock.Of<IBudgetAlertService>(), Mock.Of<IPurchaseProjectionService>(),
            Mock.Of<IReportService>(), _chartService.Object, history.Object, Mock.Of<IUnitOfWork>(), Array.Empty<IIntentHandler>());
    }

    private readonly Mock<IChartService> _chartService = new();

    private void SetupChartRequest()
    {
        _intentParser.Setup(p => p.ParseAsync(It.IsAny<MessageContent>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageIntent(IntentType.QueryChart, Chart: ChartKind.Categories));
        _chartService.Setup(c => c.BuildCategoryChartAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChartResult(new byte[] { 9, 9, 9 }, "legenda do gráfico"));
    }

    [Fact]
    public async Task HandleAsync_WhenTheReplyHasAnImage_SendsTheImageWithTheCaption()
    {
        SetupChartRequest();

        await CreateHandler().HandleAsync(new WhatsAppInboundMessage("id-img", Phone, "text", Text: "gráfico"));

        _messenger.Verify(m => m.SendImageAsync(
            Phone, It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 9, 9, 9 })), "image/png", "legenda do gráfico",
            It.IsAny<CancellationToken>()), Times.Once);
        _messenger.Verify(m => m.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenSendingTheImageFails_FallsBackToTheCaptionAsText()
    {
        SetupChartRequest();
        _messenger.Setup(m => m.SendImageAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("falhou"));

        await CreateHandler().HandleAsync(new WhatsAppInboundMessage("id-img2", Phone, "text", Text: "gráfico"));

        _messenger.Verify(m => m.SendTextAsync(Phone, "legenda do gráfico", It.IsAny<CancellationToken>()), Times.Once);
    }

    private readonly ConversationService _conversation;

    private WhatsAppMessageHandler CreateHandler() => new(_conversation, _messenger.Object);

    [Fact]
    public async Task HandleAsync_TextMessage_SendsTheConversationReplyBack()
    {
        await CreateHandler().HandleAsync(new WhatsAppInboundMessage("id1", Phone, "text", Text: "oi"));

        _messenger.Verify(m => m.SendTextAsync(Phone, "resposta da IA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AudioMessage_DownloadsTheMediaAndNormalizesTheMimeType()
    {
        _messenger.Setup(m => m.DownloadMediaAsync("media-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFile(new byte[] { 1, 2, 3 }, "audio/ogg; codecs=opus"));

        await CreateHandler().HandleAsync(new WhatsAppInboundMessage("id2", Phone, "audio", MediaId: "media-1"));

        _intentParser.Verify(p => p.ParseAsync(
            It.Is<MessageContent>(c => c.HasMedia && c.MediaMimeType == "audio/ogg"),
            It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
        _messenger.Verify(m => m.SendTextAsync(Phone, "resposta da IA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ImageWithCaption_KeepsTheCaption()
    {
        _messenger.Setup(m => m.DownloadMediaAsync("media-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFile(new byte[] { 5 }, "image/jpeg"));

        await CreateHandler().HandleAsync(new WhatsAppInboundMessage("id3", Phone, "image", MediaId: "media-2", Caption: "nota do mercado"));

        _intentParser.Verify(p => p.ParseAsync(
            It.Is<MessageContent>(c => c.Text == "nota do mercado" && c.MediaMimeType == "image/jpeg"),
            It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenMediaCannotBeDownloaded_TellsTheUserAndSkipsTheAi()
    {
        _messenger.Setup(m => m.DownloadMediaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((MediaFile?)null);

        await CreateHandler().HandleAsync(new WhatsAppInboundMessage("id4", Phone, "audio", MediaId: "media-3"));

        _messenger.Verify(m => m.SendTextAsync(Phone, It.Is<string>(t => t.Contains("arquivo")), It.IsAny<CancellationToken>()), Times.Once);
        _intentParser.Verify(p => p.ParseAsync(
            It.IsAny<MessageContent>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UnsupportedType_ExplainsWhatTheBotUnderstands()
    {
        await CreateHandler().HandleAsync(new WhatsAppInboundMessage("id5", Phone, "sticker"));

        _messenger.Verify(m => m.SendTextAsync(Phone, It.Is<string>(t => t.Contains("texto, áudio e foto")), It.IsAny<CancellationToken>()), Times.Once);
        _intentParser.Verify(p => p.ParseAsync(
            It.IsAny<MessageContent>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
