# ADR 0004: Simulação do contrato de webhook do WhatsApp nesta entrega

## Status
Aceito

## Contexto
O plano de atividades original previa apenas "estudo de tecnologias" de
WhatsApp API para este semestre, não a integração completa. A integração
real com o WhatsApp Cloud API exige: criação de app no Meta for
Developers, número de teste, tokens, e um endpoint HTTPS publicamente
acessível (via deploy ou túnel tipo ngrok) para receber o webhook.

## Alternativas consideradas
- **Integrar o WhatsApp Cloud API agora**: demonstraria o fluxo completo
  de ponta a ponta com o canal real, mas consome tempo significativo em
  configuração de infraestrutura externa (fora do controle direto do
  desenvolvimento) sem agregar ao objetivo desta entrega (base do
  sistema).
- **Não implementar nenhum endpoint de entrada de mensagens**: mais rápido,
  mas não permite demonstrar o fluxo completo (mensagem → interpretação →
  persistência → resposta) nem validar a integração com o Gemini.
- **Endpoint que replica o formato de payload do WhatsApp Cloud API**
  (`from` + `text.body`), acionável via Swagger/Postman: permite testar e
  demonstrar o fluxo completo agora, e a migração para o WhatsApp real no
  2º semestre se resume a apontar o webhook da Meta para esta mesma rota,
  sem alterar a lógica de negócio.

## Decisão
Implementar `POST /api/webhook/whatsapp` com um contrato de entrada
inspirado no payload real do WhatsApp Cloud API, sem integrar a API da
Meta nesta entrega.

## Consequências
- O fluxo de negócio completo já está validado (parser, persistência,
  resposta), reduzindo o risco técnico da integração real no 2º semestre
  a um trabalho de infraestrutura (não de lógica).
- A integração real com a Meta continua pendente e é o item de maior
  destaque nos próximos passos do projeto.
