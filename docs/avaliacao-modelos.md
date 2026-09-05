# Avaliação comparativa de modelos de IA

Executada com `tools/Nexo.GeminiEval` em 23/11/2026, com o mesmo conjunto de 39 mensagens em cada modelo.
O interpretador é o mesmo usado em produção (`GeminiIntentParser`): o modelo classifica a intenção da mensagem e extrai os dados,
e um caso só conta como acerto quando a intenção e todos os campos esperados batem. Falha do provedor (sobrecarga, rede) é tentada de novo até 3 vezes e só então contada à parte.

## Resultado geral

| Modelo | Acerto total | Acerto da intenção | Latência média | Falhas do provedor |
|---|---|---|---|---|
| `gemini-3.5-flash-lite` | 39/39 (100,0%) | 39/39 (100,0%) | 0.9 s | 0 |
| `gemini-3.1-flash-lite` | 39/39 (100,0%) | 39/39 (100,0%) | 1.0 s | 0 |
| `gemini-3.5-flash` | 39/39 (100,0%) | 39/39 (100,0%) | 2.0 s | 0 |
| `gemini-3.8-flash` | 39/39 (100,0%) | 39/39 (100,0%) | 5.6 s | 0 |
| `gemini-pro-latest` | 39/39 (100,0%) | 39/39 (100,0%) | 5.8 s | 0 |
| `gemini-3.1-pro-preview` | 39/39 (100,0%) | 39/39 (100,0%) | 5.9 s | 0 |

## Acerto por tipo de mensagem

| Modelo | Registro de transação (18) | Consulta (10) | Comando (3) | Fora do escopo (5) | Depende do contexto (3) |
|---|---|---|---|---|---|
| `gemini-3.1-flash-lite` | 18 | 10 | 3 | 5 | 3 |
| `gemini-3.5-flash-lite` | 18 | 10 | 3 | 5 | 3 |
| `gemini-3.5-flash` | 18 | 10 | 3 | 5 | 3 |
| `gemini-3.8-flash` | 18 | 10 | 3 | 5 | 3 |
| `gemini-3.1-pro-preview` | 18 | 10 | 3 | 5 | 3 |
| `gemini-pro-latest` | 18 | 10 | 3 | 5 | 3 |

## Erros por modelo

### `gemini-3.1-flash-lite`

Sem erros.

### `gemini-3.5-flash-lite`

Sem erros.

### `gemini-3.5-flash`

Sem erros.

### `gemini-3.8-flash`

Sem erros.

### `gemini-3.1-pro-preview`

Sem erros.

### `gemini-pro-latest`

Sem erros.

## Casos avaliados

**Registro de transação**

- "gastei 30 reais no mercado"
- "ganhei 500 reais hoje"
- "comprei um notebook de 3000 reais em 10 parcelas"
- "paguei 89,90 de internet"
- "recebi 1200 de freelance"
- "gastei 15 reais de uber"
- "comprei um celular em 12x de 150"
- "gastei 200 reais no cinema com a família"
- "paguei 45 reais de remédio na farmácia"
- "comprei uma bicicleta de 900 reais em 6x"
- "recebi reembolso de 120 reais"
- "ontem gastei 60 no almoço"
- "anteontem paguei 120 de luz"
- "gastei R$ 1.250,50 no aluguel"
- "paguei a assinatura do netflix, 55,90"
- "dei um presente de 150 pro meu pai"
- "pix de 35 reais pro lanche"
- "comprei uma camiseta por 80 reais"

**Consulta**

- "qual meu saldo?"
- "quanto eu tenho na conta?"
- "quanto gastei esse mês?"
- "quanto gastei com mercado esse mês?"
- "quanto gastei com transporte no mês passado?"
- "me mostra meus últimos 3 lançamentos"
- "o que eu comprei por último?"
- "consigo comprar um notebook de 4000?"
- "dá pra comprar uma tv de 2400 em 12x?"
- "como estão meus limites?"

**Comando**

- "define um limite de 500 pra mercado"
- "quero gastar no máximo 300 por mês com lazer"
- "muda o limite de transporte pra 250"

**Fora do escopo**

- "oi tudo bem?"
- "bom dia"
- "quem descobriu o brasil?"
- "me conta uma piada"
- "gastei no mercado"

**Depende do contexto**

- "e no mês passado?" (com contexto de conversa anterior)
- "e com lazer?" (com contexto de conversa anterior)
- "e mais 20 de estacionamento" (com contexto de conversa anterior)


## Leitura dos resultados

Todos os modelos acertaram os 39 casos, então neste conjunto a assertividade não separa um modelo do outro. O que muda é o tempo de resposta, de cerca de 1 s nos modelos lite a cerca de 6 s nos modelos pro, para o mesmo resultado. Para a tarefa do NEXO (classificar a mensagem e extrair os campos), o modelo mais leve já resolve.

Limitações desta avaliação:

- O conjunto tem só mensagens de texto e 39 casos, com uma execução por modelo. Como a IA não é determinística, diferenças pequenas não seriam detectadas.
- Áudio e foto de nota fiscal foram testados manualmente (um caso de cada) e não entram nesta tabela.
- Os modelos `gemini-2.5-flash-lite`, `gemini-2.5-flash` e `gemini-2.5-pro` foram descartados da comparação porque a API os recusa para contas novas (erro 404), por isso a comparação usa a linha 3.x.
