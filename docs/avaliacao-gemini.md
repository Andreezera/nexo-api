# Avaliação de acurácia — extração estruturada via Gemini

Executada com `tools/Nexo.GeminiEval` em 23/07/2026, usando o modelo
`gemini-flash-latest` (resolvido para `gemini-3.6-flash` no momento do teste).

## Resultado

| Mensagem | Esperado | Obtido | Resultado |
|---|---|---|---|
| gastei 30 reais no mercado | Expense/30 | Expense/30 | OK |
| ganhei 500 reais hoje | Income/500 | Income/500 | OK |
| comprei um notebook de 3000 reais em 10 parcelas | Expense/3000/10x | Expense/3000/10x | OK |
| paguei 89,90 de internet | Expense/89,90 | Expense/89,90 | OK |
| recebi 1200 de freelance | Income/1200 | Income/1200 | OK |
| gastei 15 reais de uber | Expense/15 | Expense/15 | OK |
| comprei um celular em 12x de 150 | Expense/1800/12x | Expense/1800/12x | OK |
| oi tudo bem? | não-financeiro | não-financeiro | OK |
| quanto posso gastar esse mês? | não-financeiro | não-financeiro | OK |
| recebi 50 reais de aniversário | Income/50 | não avaliado (cota) | — |
| gastei 200 reais no cinema com a familia | Expense/200 | Expense/200 | OK |
| paguei 45 reais de remedio na farmacia | Expense/45 | não avaliado (cota) | — |
| comprei uma bicicleta de 900 reais em 6x | Expense/900 | não avaliado (cota) | — |
| bom dia | não-financeiro | não-financeiro | OK |
| recebi reembolso de 120 reais | Income/120 | não avaliado (cota) | — |

**11 de 15 casos avaliados dentro da cota gratuita disponível — 100% de
acerto entre eles**, incluindo o caso que exige o modelo derivar o valor
total a partir do valor da parcela (`150 x 12 = 1800`).

## Achado relevante: limite de cota diária

Os 4 casos restantes não retornaram classificação — retornaram erro HTTP
429 (`RESOURCE_EXHAUSTED`), reportando o limite real do tier gratuito do
modelo `gemini-3.6-flash`:

```
quotaId: GenerateRequestsPerDayPerProjectPerModel-FreeTier
quotaValue: 20
```

Ou seja, o tier gratuito deste modelo específico permite **apenas 20
requisições por dia** por projeto — um limite mais restritivo do que o
documentado de forma genérica na página de rate limits do Google, e que só
foi descoberto através de teste empírico. Nenhuma das falhas foi causada
por classificação incorreta do modelo.

## Implicação para o 2º semestre

Se o volume de mensagens crescer (múltiplos usuários testando, ou um
ambiente de demonstração para a banca com várias interações), a cota
gratuita diária de um único modelo pode não ser suficiente. Estratégias a
avaliar: (a) implementar retry com backoff respeitando o header
`retryDelay` da resposta de erro, (b) fazer fallback entre modelos
(`gemini-flash-latest` → `gemini-flash-lite-latest`, com cotas
independentes), ou (c) migrar para o tier pago caso o uso justifique.
