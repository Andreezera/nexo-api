using System.Text;
using Nexo.Api.WhatsApp;
using Nexo.Application.Messaging;

namespace Nexo.Tests.Api;

public class InteractiveMessagesTests
{
    private static byte[] Payload(string messageJson) => Encoding.UTF8.GetBytes(
        "{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[" + messageJson + "]}}]}]}");

    [Fact]
    public void Parse_ButtonReply_BecomesThePhraseOfThatCommand()
    {
        var body = Payload("{\"from\":\"5511\",\"id\":\"m1\",\"type\":\"interactive\",\"interactive\":{\"type\":\"button_reply\",\"button_reply\":{\"id\":\"cmd_undo\",\"title\":\"Desfazer\"}}}");

        var message = Assert.Single(WhatsAppCloudPayloadParser.Parse(body));

        Assert.Equal("text", message.Type);
        Assert.Equal("apaga o último", message.Text);
        Assert.Equal("5511", message.From);
    }

    [Fact]
    public void Parse_ListReply_BecomesThePhraseOfThatCommand()
    {
        var body = Payload("{\"from\":\"5511\",\"id\":\"m2\",\"type\":\"interactive\",\"interactive\":{\"type\":\"list_reply\",\"list_reply\":{\"id\":\"cmd_grafico_categoria\",\"title\":\"Gráfico por categoria\"}}}");

        var message = Assert.Single(WhatsAppCloudPayloadParser.Parse(body));

        Assert.Equal("text", message.Type);
        Assert.Equal("gráfico por categoria", message.Text);
    }

    [Fact]
    public void Parse_UnknownCommandId_FallsBackToTheTitleTheUserSaw()
    {
        var body = Payload("{\"from\":\"5511\",\"id\":\"m3\",\"type\":\"interactive\",\"interactive\":{\"type\":\"button_reply\",\"button_reply\":{\"id\":\"cmd_que_nao_existe\",\"title\":\"Meu saldo\"}}}");

        var message = Assert.Single(WhatsAppCloudPayloadParser.Parse(body));

        Assert.Equal("Meu saldo", message.Text);
    }

    [Fact]
    public void Parse_InteractiveWithoutAReply_IsKeptAsAnUnsupportedType()
    {
        var body = Payload("{\"from\":\"5511\",\"id\":\"m4\",\"type\":\"interactive\",\"interactive\":{\"type\":\"nfm_reply\"}}");

        var message = Assert.Single(WhatsAppCloudPayloadParser.Parse(body));

        Assert.Equal("interactive", message.Type);
        Assert.Null(message.Text);
    }

    [Fact]
    public void MainMenu_RespectsTheMetaLimits()
    {
        var menu = InteractiveCommands.MainMenu;
        var rows = menu.Sections.SelectMany(s => s.Rows).ToList();

        Assert.True(rows.Count <= 10);
        Assert.True(menu.Body.Length <= 1024);
        Assert.True(menu.ButtonLabel.Length <= 20);
        Assert.True(menu.Footer!.Length <= 60);
        Assert.All(menu.Sections, s => Assert.True(s.Title.Length <= 24));
        Assert.All(rows, r => Assert.True(r.Title.Length <= 24, r.Title));
        Assert.All(rows, r => Assert.True(r.Description!.Length <= 72, r.Description));
        Assert.Equal(rows.Count, rows.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public void EveryMenuRowAndButton_HasAPhraseToRun()
    {
        var ids = InteractiveCommands.MainMenu.Sections.SelectMany(s => s.Rows).Select(r => r.Id)
            .Concat(InteractiveCommands.AfterRegistration.Select(b => b.Id));

        Assert.All(ids, id => Assert.NotNull(InteractiveCommands.PhraseFor(id)));
    }

    [Fact]
    public void AfterRegistration_HasAtMostThreeButtonsWithShortTitles()
    {
        var buttons = InteractiveCommands.AfterRegistration;

        Assert.InRange(buttons.Count, 1, 3);
        Assert.All(buttons, b => Assert.True(b.Title.Length <= 20, b.Title));
    }

    [Fact]
    public void PhraseFor_UnknownOrNullId_IsNull()
    {
        Assert.Null(InteractiveCommands.PhraseFor("nada"));
        Assert.Null(InteractiveCommands.PhraseFor(null));
    }
}
