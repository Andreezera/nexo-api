# ADR 0002: Escolha do Gemini como provedor de LLM para extração de dados

## Status
Aceito

## Contexto
O NEXO precisa interpretar mensagens em linguagem natural ("gastei 30 reais
no mercado") e extrair dados estruturados (valor, tipo, categoria,
descrição, parcelamento). A integração completa de IA conversacional está
planejada para o 2º semestre; nesta etapa, o objetivo é validar o uso de
LLM para uma tarefa pontual de extração estruturada.

## Alternativas consideradas
- **OpenAI (GPT)**: API madura e bem documentada, mas sem tier gratuito
  utilizável sem cartão de crédito cadastrado — incompatível com a
  restrição de orçamento zero do projeto.
- **Anthropic (Claude)**: mesma limitação — acesso via API exige conta com
  billing configurado, sem tier gratuito para uso contínuo.
- **Google Gemini (via Google AI Studio)**: possui tier gratuito real, sem
  necessidade de cartão de crédito, com suporte nativo a *structured
  output* (resposta em JSON validado contra um schema), o que reduz a
  fragilidade de fazer parsing de texto livre gerado por um LLM.
- **Parser baseado em regras (regex/palavras-chave)**: custo zero e sem
  dependência externa, mas não atende ao objetivo de aprendizado do TCC
  (uso de IA) nem generaliza bem para frases fora dos padrões previstos.

## Decisão
Usar a API do Gemini (Google AI Studio) com *structured output*
(`responseSchema`) para a extração de dados da mensagem, isolada atrás da
interface `ITransactionMessageParser`.

## Consequências
- Dependência de disponibilidade e política de cota gratuita do Google, que
  já se mostrou instável na prática: o modelo `gemini-2.0-flash` estava com
  cota gratuita zerada para chaves de API novas no momento dos testes
  (ver seção de dificuldades no relatório). A mitigação foi usar o alias
  `gemini-flash-latest`, que sempre aponta para o modelo *flash* vigente,
  em vez de fixar uma versão específica.
- Por estar isolado atrás de uma interface (`ITransactionMessageParser`),
  trocar de provedor ou adicionar um modelo mais avançado no 2º semestre
  não exige mudanças em nenhuma outra camada do sistema.
