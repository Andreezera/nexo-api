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

## Medição com áudio e foto, e troca do modelo padrão

Depois de testes reais pelo WhatsApp, uma resposta com áudio levou cerca de 12 s, e uma de texto cerca de 6 s. Para reduzir isso foram medidos quatro modelos com texto, áudio e foto de nota fiscal (3 execuções cada, mediana abaixo). As entradas foram uma mensagem de texto, um áudio sintético e duas notas fiscais reais (uma com 1 item e uma de supermercado com 9 itens). O prompt dessa medição é uma versão simplificada do usado no NEXO, então os tempos servem para comparar os modelos entre si.

| Modelo | Texto | Áudio | Foto (1 item) | Foto (9 itens) | Acertos |
|---|---|---|---|---|---|
| gemini-flash-latest (anterior) | 6,9 s | 5,0 s | 6,1 s | 5,0 s | 12/12 |
| gemini-3.5-flash | 3,3 s | 3,2 s | 3,6 s | 4,1 s | 12/12 |
| gemini-3.1-flash-lite | 0,9 s | 1,8 s | 1,9 s | 1,7 s | 12/12 |
| gemini-3.5-flash-lite | 0,8 s | 1,7 s | 1,9 s | 1,7 s | 12/12 |
| gemini-flash-lite-latest | 0,8 s | 1,5 s | 1,6 s | 1,7 s | 12/12 |

O apelido `gemini-flash-lite-latest` também foi executado no conjunto completo de 39 casos, com o prompt real do NEXO, e acertou 39/39 (intenção 39/39) com latência média de 0,8 s.

Decisão: o modelo padrão passou de `gemini-flash-latest` para `gemini-flash-lite-latest`. O apelido foi preferido a um nome fixo porque nomes fixos de modelo são aposentados pelo provedor. O ganho foi de 3 a 6 vezes no tempo da chamada à IA, sem queda de acerto nos testes feitos. A limitação é a mesma de antes, os conjuntos são pequenos e com poucas execuções.


## Ampliação do conjunto para 78 casos

Os recursos novos de conversa (menu, resumo por mês, gráficos, desfazer e corrigir, contas recorrentes, "como estou no mês",
perguntas livres e metas de economia) ganharam casos próprios, e o conjunto passou de 39 para 78 mensagens. Os casos conferem
a intenção e também os campos que a IA extrai, como a palavra procurada, o período (por exemplo, "esse ano" começa em 1º de
janeiro), o tipo (gasto ou receita), a agregação (soma, quantidade, média, maior ou lista) e o prazo de uma meta.

Com o modelo padrão `gemini-flash-lite-latest`, o resultado foi de 78 acertos em 78, com latência média de 1,0 s. No caminho,
a avaliação encontrou erros reais que foram corrigidos:

- Em "em outubro", a IA preenchia só a data de início, o que faria a busca ir até hoje. As datas passaram a ser campos obrigatórios.
- Sem período na pergunta, a IA inventava "desde o dia 1º do mês". O prompt passou a mandar deixar as datas vazias.
- A IA sugeria uma categoria (como "Outros" ou "Alimentação") que escondia lançamentos. Com uma palavra-chave, a categoria sugerida é ignorada.
- Em "quero juntar 3000", a IA usava o verbo "juntar" como nome da meta. O mapeamento passou a descartar verbos como nome.
- A temperatura da IA na interpretação foi reduzida a zero, para diminuir a variação entre execuções.

A limitação continua sendo a de antes: o conjunto é pequeno e a IA não é determinística, então um caso raro pode errar numa
execução e acertar em outra. Durante os ajustes, algumas execuções erraram 1 caso em 78 antes da última correção.
