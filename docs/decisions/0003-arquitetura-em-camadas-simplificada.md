# ADR 0003: Arquitetura em camadas simplificada (Clean Architecture sem CQRS/generic repository)

## Status
Aceito

## Contexto
O projeto precisa de uma arquitetura que isole regras de negócio de
detalhes técnicos voláteis (banco de dados, provedor de IA, formato de
mensagens do WhatsApp), permitindo trocar esses detalhes no futuro (ex:
IA mais avançada e WhatsApp real no 2º semestre) sem reescrever a camada de
domínio.

## Alternativas consideradas
- **Clean Architecture completa com CQRS + MediatR**: seria a escolha
  natural para um sistema de maior porte com múltiplos desenvolvedores.
  Para um projeto solo de um semestre, a cerimônia extra (handlers,
  pipelines de mediator, separação de comandos/queries) não se paga —
  adiciona indireção sem benefício correspondente no escopo atual.
- **Repository genérico (`IRepository<T>`)**: reduz duplicação de código,
  mas esconde intenção de domínio (ex: `GetByPhoneNumberAsync` é mais
  explícito que uma query genérica) e é conhecido por vazar detalhes de
  ORM quando o projeto cresce.
- **Arquitetura em 4 camadas simplificada** (`Domain → Application →
  Infrastructure/Api`), com repositórios específicos por agregado e um
  `IUnitOfWork` simples: mantém a inversão de dependência (a Application
  não conhece EF Core; a Infrastructure implementa as interfaces da
  Application) sem introduzir abstrações que não são usadas.

## Decisão
Adotar a arquitetura em 4 camadas simplificada, com repositórios
específicos (`IUserRepository`, `ITransactionRepository`,
`IInstallmentPlanRepository`) e `IUnitOfWork`, evitando CQRS/MediatR e
repositório genérico.

## Consequências
- Menos código boilerplate para o escopo atual (1 semestre, 1
  desenvolvedor).
- Se o projeto crescer significativamente no 2º semestre (múltiplos
  módulos de IA, mais entidades), pode valer reavaliar a introdução de
  CQRS — decisão que fica documentada aqui para revisão futura, não
  descartada permanentemente.
