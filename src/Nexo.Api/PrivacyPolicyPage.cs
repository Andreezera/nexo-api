namespace Nexo.Api;

// Página de política de privacidade exigida pela Meta para publicar o app do WhatsApp.
public static class PrivacyPolicyPage
{
    public const string Html = """
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>NEXO - Política de Privacidade</title>
          <style>
            body { font-family: Arial, sans-serif; max-width: 720px; margin: 40px auto; padding: 0 16px; line-height: 1.6; color: #1a2e2f; }
            h1 { font-size: 26px; }
            h2 { font-size: 18px; margin-top: 28px; }
          </style>
        </head>
        <body>
          <h1>NEXO - Política de Privacidade</h1>
          <p>O NEXO é um assistente financeiro pelo WhatsApp desenvolvido como projeto acadêmico (Projetos em Computação, UNESP Rio Claro). Esta página explica quais dados ele usa.</p>

          <h2>Dados que o NEXO guarda</h2>
          <ul>
            <li>O número de telefone de quem conversa com o NEXO no WhatsApp.</li>
            <li>O texto das mensagens enviadas e das respostas do sistema, usados como contexto da conversa.</li>
            <li>As transações financeiras que o usuário registra (valor, categoria, descrição e data) e os limites de gasto que ele define.</li>
          </ul>

          <h2>Áudios e fotos</h2>
          <p>Áudios e fotos enviados são usados apenas para extrair os dados da transação. O arquivo é enviado ao serviço de IA Google Gemini para ser interpretado e não é armazenado pelo NEXO.</p>

          <h2>Para que os dados são usados</h2>
          <p>Somente para registrar as transações do próprio usuário, responder às suas perguntas e calcular limites e projeções. Os dados não são vendidos nem compartilhados com terceiros, exceto com o Google (interpretação das mensagens) e a Meta (envio e recebimento pelo WhatsApp).</p>

          <h2>Exclusão dos dados</h2>
          <p>O usuário pode pedir a exclusão dos seus dados a qualquer momento pelo e-mail andre.dionisio@unesp.br.</p>

          <h2>Contato</h2>
          <p>André Augusto Costa Dionísio, andre.dionisio@unesp.br.</p>
        </body>
        </html>
        """;
}
