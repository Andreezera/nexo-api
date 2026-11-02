namespace Nexo.Application.Messaging;

// Botões de resposta rápida (até 3, título de até 20 caracteres) que aparecem embaixo de uma mensagem.
public record ReplyButton(string Id, string Title);

public record ListRow(string Id, string Title, string? Description = null);

public record ListSection(string Title, IReadOnlyList<ListRow> Rows);

// Mensagem de lista: um botão que abre as opções para tocar (até 10 linhas no total, título de até 24 caracteres).
public record InteractiveList(string Body, string ButtonLabel, IReadOnlyList<ListSection> Sections, string? Footer = null);

// Cada botão ou linha de lista tem um id. Ao tocar, a Meta devolve esse id, e aqui ele vira a frase que o usuário
// escreveria, que segue o caminho normal da conversa. Assim tocar e digitar fazem exatamente a mesma coisa.
public static class InteractiveCommands
{
    public const string Undo = "cmd_undo";
    public const string Balance = "cmd_saldo";
    public const string Summary = "cmd_resumo";

    private static readonly Dictionary<string, string> Phrases = new()
    {
        [Undo] = "apaga o último",
        [Balance] = "qual meu saldo?",
        [Summary] = "resumo do mês",
        ["cmd_categorias"] = "gastos por categoria",
        ["cmd_ultimos"] = "meus últimos lançamentos",
        ["cmd_grafico_categoria"] = "gráfico por categoria",
        ["cmd_grafico_meses"] = "gráfico dos últimos meses",
        ["cmd_mes"] = "como estou esse mês?",
        ["cmd_limites"] = "como estão meus limites?",
        ["cmd_metas"] = "como estão minhas metas?",
        ["cmd_contas"] = "minhas contas fixas"
    };

    public static string? PhraseFor(string? id) => id is not null && Phrases.TryGetValue(id, out var phrase) ? phrase : null;

    // Botões que acompanham a confirmação de um registro.
    public static IReadOnlyList<ReplyButton> AfterRegistration { get; } = new[]
    {
        new ReplyButton(Undo, "↩️ Desfazer"),
        new ReplyButton(Balance, "💼 Saldo"),
        new ReplyButton(Summary, "📊 Resumo do mês")
    };

    // Menu principal. O texto de registrar fica no corpo, porque registrar exige escrever (não dá para tocar).
    public static InteractiveList MainMenu { get; } = new(
        Body: "👋 Oi! Eu sou o *NEXO*, seu assistente financeiro.\n\n"
              + "📝 *Para registrar*, é só escrever:\n"
              + "• gastei 30 no mercado\n"
              + "• recebi 2500 de salário\n"
              + "• netflix 55,90 todo dia 10\n"
              + "• 🎤 um áudio ou 📸 a foto de uma nota fiscal\n\n"
              + "👇 Para consultar, toque em *Ver opções*. Você também pode me perguntar com as suas palavras, "
              + "por exemplo \"quanto gastei com uber esse ano?\".",
        ButtonLabel: "Ver opções",
        Sections: new[]
        {
            new ListSection("Consultar", new[]
            {
                new ListRow(Balance, "Saldo", "Quanto você tem hoje"),
                new ListRow(Summary, "Resumo do mês", "Receitas, despesas e comparação"),
                new ListRow("cmd_categorias", "Gastos por categoria", "Onde o dinheiro foi"),
                new ListRow("cmd_ultimos", "Últimos lançamentos", "Os 5 mais recentes")
            }),
            new ListSection("Gráficos", new[]
            {
                new ListRow("cmd_grafico_categoria", "Gráfico por categoria", "Imagem dos gastos do mês"),
                new ListRow("cmd_grafico_meses", "Gráfico dos meses", "Receitas x despesas")
            }),
            new ListSection("Planejar", new[]
            {
                new ListRow("cmd_mes", "Como estou no mês", "Quanto ainda dá para gastar por dia"),
                new ListRow("cmd_limites", "Meus limites", "Limites de gasto por categoria"),
                new ListRow("cmd_metas", "Minhas metas", "Progresso das metas de economia"),
                new ListRow("cmd_contas", "Contas fixas", "Assinaturas e contas recorrentes")
            })
        },
        Footer: "Digite menu para voltar aqui");
}
