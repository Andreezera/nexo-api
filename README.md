# NEXO, Assistente Financeiro Inteligente via WhatsApp

Trabalho de Conclusão de Curso (Bacharelado em Ciência da Computação, UNESP Rio Claro), disciplinas *Projetos em Computação I* (1º semestre de 2026) e *Projetos em Computação II* (2º semestre de 2026).

**Autor:** André Augusto Costa Dionísio
**Orientador:** Prof. Denis Henrique Pinheiro Salvadeo (DEMAC)

## O que é

O NEXO é um assistente financeiro que funciona dentro do WhatsApp. O usuário escreve (ou fala, ou manda a foto de uma nota fiscal) e uma IA entende o que ele quer. Pode ser registrar um gasto, uma receita ou uma compra parcelada, mas também perguntar quanto gastou, ver o saldo, definir um limite de gasto ou simular uma compra.

Exemplos de mensagens que o sistema entende:

```
"gastei 30 reais no mercado"
"comprei um notebook de 3000 reais em 10 parcelas"
"quanto gastei com mercado esse mês?"
"e no mês passado?"
"define um limite de 500 para lazer"
"consigo comprar uma tv de 2400 em 12x?"
```

## Funcionalidades

**Etapa 1 (Projetos em Computação I)**
- Registro de despesas e receitas em linguagem natural, com extração estruturada via Gemini
- Compras parceladas, com a divisão em parcelas feita pelo próprio sistema
- Categorias normalizadas (enum), edição e remoção lógica de lançamentos com auditoria
- Extração da data real da mensagem ("ontem", "sexta passada")

**Etapa 2 (Projetos em Computação II)**
- Limites de gasto por categoria, com alerta junto da resposta ao passar de 80% e ao estourar
- Projeção de capacidade de compra, considerando o saldo, a média dos últimos meses e as parcelas já comprometidas
- IA conversacional, que usa as últimas mensagens da conversa como contexto e responde perguntas sobre os dados do usuário
- Registro de transações por áudio e por foto de nota fiscal
- Integração com o WhatsApp Cloud API (webhook com assinatura, fila em segundo plano, envio da resposta)
- Avaliação comparativa de modelos de IA ([docs/avaliacao-modelos.md](docs/avaliacao-modelos.md))

**Experiência no WhatsApp**
- Menu de ajuda (`menu`, `ajuda`, ou um "oi"), e dica do menu no primeiro contato
- Respostas formatadas com emojis, negrito e barras de progresso (registro, saldo, limites, últimos lançamentos)
- Resumo por mês ("resumo do mês", "resumo de outubro"): receitas, despesas, saldo, gastos por categoria e comparação com o mês anterior
- Gráficos como imagem (por categoria e receitas x despesas dos últimos meses), desenhados no servidor com SkiaSharp
- Desfazer e corrigir o último lançamento ("apaga o último", "corrige para 25")
- Endpoints de relatórios (`/api/users/{telefone}/reports/...`) com os mesmos dados e gráficos, para usar fora do WhatsApp

## Arquitetura

Solução em 4 camadas, inspirada em Clean Architecture, de forma deliberadamente simplificada (sem CQRS, sem MediatR e sem repositório genérico):

![Diagrama de arquitetura](docs/diagrams/architecture-diagram.png)

| Projeto | Responsabilidade |
|---|---|
| `Nexo.Domain` | Entidades e regras de negócio puras (`User`, `Transaction`, `InstallmentPlan`, `BudgetLimit`, `ConversationMessage`) |
| `Nexo.Application` | Orquestração (`ConversationService`, `MessageProcessingService`, `BudgetAlertService`, `PurchaseProjectionService`) e abstrações |
| `Nexo.Infrastructure` | EF Core + PostgreSQL (Neon), `GeminiIntentParser`, `WhatsAppCloudMessenger` |
| `Nexo.Api` | Controllers REST, webhook do WhatsApp, fila em segundo plano, Swagger |

Modelo de dados: [docs/diagrams/er-diagram.png](docs/diagrams/er-diagram.png)
Fluxo de uma mensagem: [docs/diagrams/sequence-diagram.png](docs/diagrams/sequence-diagram.png)
Visão do projeto: [docs/diagrams/roadmap-diagram.png](docs/diagrams/roadmap-diagram.png)

## Tecnologias

- .NET 10 / ASP.NET Core
- Entity Framework Core + Npgsql (PostgreSQL via [Neon](https://neon.tech), serverless)
- Google Gemini API (saída estruturada por JSON schema, entrada de texto, áudio e imagem)
- WhatsApp Cloud API (Meta)
- xUnit + Moq (testes automatizados)
- Swagger / Swashbuckle

## Como rodar localmente

Pré-requisitos: .NET 10 SDK, uma instância PostgreSQL (ex: Neon, gratuito) e uma chave de API do Gemini ([aistudio.google.com](https://aistudio.google.com)). O uso de áudio e imagem em volume, e os testes de avaliação, pedem o faturamento ativado na chave.

```bash
# Configurar segredos (nunca comitados)
dotnet user-secrets set "ConnectionStrings:Default" "<sua connection string>" --project src/Nexo.Api
dotnet user-secrets set "Gemini:ApiKey" "<sua chave>" --project src/Nexo.Api

# Aplicar migrations
dotnet ef database update --project src/Nexo.Infrastructure --startup-project src/Nexo.Api

# Rodar a API
dotnet run --project src/Nexo.Api
```

Depois de rodar, abra **https://localhost:7117/index.html** no navegador. O Swagger UI abre com todos os endpoints documentados e testáveis por ali mesmo. (Evite acessar pela porta http 5161. A spec do Swagger é montada em cima da porta https, então testar "Try it out" a partir da 5161 dispara um erro de CORS por serem origens diferentes.)

### Testando pelo Swagger ou curl

O endpoint abaixo simula uma mensagem de texto do WhatsApp. Via `curl` não há restrição de CORS (é uma limitação só de navegador), então qualquer uma das portas funciona:

```bash
curl -X POST http://localhost:5161/api/webhook/whatsapp \
  -H "Content-Type: application/json" \
  -d '{"from":"5511999999999","text":{"body":"gastei 30 reais no mercado"}}'
```

Para testar áudio ou foto de nota fiscal, use o endpoint de upload (no Swagger aparece um seletor de arquivo):

```bash
curl -X POST http://localhost:5161/api/webhook/whatsapp/media \
  -F "from=5511999999999" \
  -F "file=@nota-fiscal.jpg;type=image/jpeg"
```

Outros endpoints:

| Método | Rota | O que faz |
|---|---|---|
| GET | `/api/users/{telefone}/transactions` | Lista os lançamentos |
| GET | `/api/users/{telefone}/transactions/summary` | Receita total, despesa total e saldo |
| PUT / DELETE | `/api/transactions/{id}` | Corrige ou remove (remoção lógica) um lançamento |
| GET | `/api/users/{telefone}/budget-limits` | Limites de gasto do mês, com quanto já foi gasto |
| PUT / DELETE | `/api/users/{telefone}/budget-limits/{categoria}` | Define ou remove o limite de uma categoria |
| GET | `/api/users/{telefone}/purchase-projection?price=3000&installments=10` | Simula uma compra |

### WhatsApp de verdade

O endpoint real da Meta é `/api/webhook/whatsapp/cloud` (verificação por GET e mensagens por POST, com assinatura HMAC conferida). Ele lê estes segredos:

```bash
dotnet user-secrets set "WhatsApp:AccessToken" "<token>" --project src/Nexo.Api
dotnet user-secrets set "WhatsApp:PhoneNumberId" "<phone number id>" --project src/Nexo.Api
dotnet user-secrets set "WhatsApp:VerifyToken" "<texto que você escolher>" --project src/Nexo.Api
dotnet user-secrets set "WhatsApp:AppSecret" "<app secret do app na Meta>" --project src/Nexo.Api
```

Como a Meta precisa alcançar a API pela internet, é preciso um túnel (por exemplo `ngrok http 5161`). No painel do app, a Callback URL é `https://<seu-tunel>/api/webhook/whatsapp/cloud`, com o mesmo token de verificação, e o campo `messages` assinado. A versão da Graph API pode ser trocada em `WhatsApp:GraphApiVersion`.

Para subir tudo de uma vez (API, túnel Cloudflare e atualização automática do webhook da Meta), use `scripts/iniciar-demo.ps1`. O passo a passo para rodar em outra máquina está em [docs/rodar-no-notebook.md](docs/rodar-no-notebook.md).

## Testes

```bash
dotnet test tests/Nexo.Tests
```

Os testes cobrem as regras do domínio (divisão de parcelas, remoção lógica, limites), os serviços de aplicação (conversa, alertas de limite, projeção de compra, tratamento das mensagens do WhatsApp), o mapeamento da resposta da IA, o cliente do WhatsApp (com um `HttpMessageHandler` falso), a validação da assinatura da Meta e os controllers.

A avaliação de modelos de IA (`tools/Nexo.GeminiEval`) não faz parte da suíte automática por depender de rede real. Ela roda o mesmo conjunto de 54 mensagens em vários modelos do Gemini e grava o resultado em [`docs/avaliacao-modelos.md`](docs/avaliacao-modelos.md). Com `--sem-relatorio`, roda sem sobrescrever esse documento. A avaliação da primeira etapa continua em [`docs/avaliacao-gemini.md`](docs/avaliacao-gemini.md).

## Estrutura do repositório

```
src/
  Nexo.Domain/          entidades e regras de negócio
  Nexo.Application/     orquestração e abstrações
  Nexo.Infrastructure/  EF Core, repositórios, Gemini, WhatsApp
  Nexo.Api/             controllers, webhook, fila, Swagger
tests/
  Nexo.Tests/           testes automatizados (xUnit + Moq)
tools/
  Nexo.GeminiEval/      avaliação comparativa de modelos (manual)
docs/
  diagrams/              diagramas de arquitetura, modelo de dados, fluxo e visão do projeto
  avaliacao-gemini.md    avaliação da etapa 1
  avaliacao-modelos.md   avaliação comparativa de modelos (etapa 2)
```

## Próximos passos

Ideias que ficaram fora desta etapa: leitura de extratos bancários em PDF, status do parcelamento (quitação antecipada e cancelamento) com suporte a juros, e alertas proativos (avisar sem o usuário perguntar).
