# ADR 0001: Escolha do Neon como provedor de PostgreSQL

## Status
Aceito

## Contexto
O projeto precisa de um banco relacional para dados financeiros estruturados
(usuários, transações, parcelamentos), com custo zero — o projeto não tem
orçamento e é desenvolvido por um único aluno.

## Alternativas consideradas
- **Supabase**: também oferece PostgreSQL gratuito, mas o foco do produto é
  mais amplo (auth, storage, realtime), trazendo complexidade desnecessária
  para o escopo atual.
- **Railway**: tier gratuito existe, mas historicamente mais instável em
  limites de uso gratuito e sujeito a mudanças de política.
- **PostgreSQL local/Docker**: sem custo, mas não demonstra uma arquitetura
  hospedada em nuvem (um dos objetivos declarados no plano de atividades:
  "computação em nuvem").
- **Neon**: PostgreSQL serverless, tier gratuito estável, connection string
  compatível com qualquer client Npgsql/EF Core sem adaptação, sem
  necessidade de cartão de crédito.

## Decisão
Utilizar Neon como instância de PostgreSQL para todos os ambientes do
projeto neste semestre.

## Consequências
- Nenhuma mudança de código necessária — Neon é PostgreSQL padrão, então
  `Npgsql.EntityFrameworkCore.PostgreSQL` funciona sem adaptação.
- Dependência de um serviço externo gratuito; se os limites do tier gratuito
  mudarem no futuro, a migração para outro provedor Postgres é trivial
  (troca de connection string).
