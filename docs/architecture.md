# CyberProtech Web Security Lab — Visão da Arquitetura (Fase 1)

```text
                +------------------+
                |    frontend      |  React + Vite (nginx em produção)
                |  localhost:5173  +------+
                +--------+---------+      |
                         | HTTP (VITE_API_BASE_URL) /api/*
                         v                |
                +--------+---------+      |  relatórios JSON (Fase 4)
                |     backend      |      |
                |  ASP.NET Core 8  +------+
                |  localhost:5000  |      |
                +--------+---------+      |
                         | EF Core (Npgsql) |
                         v                |
                +--------+---------+      |
                |    PostgreSQL    |      |
                |  (db:5432, vol   |      |
                |   nomeado pgdata)|      |
                +------------------+      |
                                          v
                                 +--------+---------+
                                 |     toolkit      |  Python (perfil sob demanda)
                                 |  headers/cookies/|
                                 |  endpoints/config|
                                 +------------------+
```

## Camadas do backend

```text
backend/API            -> Controllers, Endpoints (health), Handlers (erros, policies), Program.cs, Swagger
backend/Application    -> DTOs + Interfaces (contratos, sem dependência de infra)
backend/Domain         -> Entidades (ex.: User)
backend/Infrastructure -> EF Core + PostgreSQL (AppDbContext, Migrations, repositórios, seed)
                         + Security (BCrypt, JWT)
```

Dependências apontam para dentro: `API -> Application, Infrastructure`;
`Infrastructure -> Application, Domain`. `Domain` não depende de ninguém.

Detalhes de setup, comandos de migration e a tabela de endpoints estão em
`backend/README.md`.

## Banco de dados

- EF Core 8 com provider Npgsql. A modelagem fica em
  `Infrastructure/Persistence/AppDbContext.cs`.
- Migrations versionadas em `Infrastructure/Persistence/Migrations/`
  (`InitialCreate` cria `users` com índices únicos em `Username` e `Email`).
- `AppDbContextFactory` implementa `IDesignTimeDbContextFactory`: o `dotnet ef`
  monta o contexto sem subir a API, então gerar migration não exige banco no ar.
- Na inicialização a API aplica as migrations pendentes e popula o seed
  (`DatabaseInitializer`), controlado por `Database:MigrateOnStartup` e
  `Database:Seed`. O seed só age se a tabela `users` estiver vazia.
- Os Ids e as datas do seed são fixos e iguais aos dados de exemplo do frontend,
  para a demonstração ficar coerente com `VITE_USE_SAMPLE_DATA=false`.
- Todos os usuários do seed compartilham a senha de `Seed:Password` (padrão
  `CyberProtech@2026`), gravada com BCrypt.

## Autenticação e autorização

```text
POST /api/auth/register  -> cria User e devolve token
POST /api/auth/login     -> confere BCrypt e devolve token
GET  /api/auth/me        -> usuário do token

JwtTokenService  -> HS256; claims sub, nameidentifier, name, email, role
IPasswordHasher  -> BCrypt custo 12, sal por senha
```

O middleware roda nesta ordem: `UseCors` -> `UseAuthentication` ->
`UseAuthorization` -> `UseRateLimiter`.

| Policy         | Requisito              | Onde                                              |
|----------------|------------------------|---------------------------------------------------|
| `Authenticated` | usuário autenticado    | padrão de `UsersController`, `GET /api/auth/me`    |
| `AdminOnly`     | `RequireRole("Admin")` | `PUT` e `DELETE /api/users/{id}`                  |

Além da policy, `GET /api/users/{id}` compara o `id` da rota com o
`NameIdentifier` do token: Admin vê qualquer um, usuário comum só o próprio.

Login tem rate limit de 10 tentativas por minuto por IP (janela fixa em memória,
`429` ao estourar). É um freio para a demonstração, não um controle de
produção: em múltiplas instâncias o limite é por processo e se perde no restart.

Controles que já valem para a Fase 2, e que a Fase 3 vai remover de propósito
para servirem de laboratório:

- auto-cadastro sempre cria `User` — o corpo não escolhe o papel;
- `PUT /api/users/{id}` não aceita `role` nem senha;
- mensagem de login única e verificação de hash mesmo sem usuário, para não
  revelar existência de conta por mensagem nem por tempo de resposta;
- token validado por assinatura: editar o payload devolve `401`.

## Tratamento de erros

`GlobalExceptionHandler` converte exceções não tratadas em ProblemDetails
(RFC 9457). O detalhe técnico vai para o log; a resposta traz apenas título,
`traceId` e uma mensagem genérica. Exceções de banco viram 409, sem expor SQL.

## Health checks

| Rota                 | Checagem                    | Quando falha         |
|----------------------|-----------------------------|----------------------|
| `/api/health`        | nada (só o processo)        | nunca, se subiu      |
| `/api/health/ready`  | `SELECT 1` no PostgreSQL    | banco inacessível    |

O Compose usa `/api/health` no `healthcheck` do container, e o frontend consome
`/api/health` para o indicador de status.

## Configuração

| Chave                             | Padrão                 | Efeito                                  |
|-----------------------------------|------------------------|-----------------------------------------|
| `ConnectionStrings:DefaultConnection` | credencial de exemplo | conexão Npgsql                          |
| `Database:MigrateOnStartup`       | `true`                 | aplica migrations ao subir              |
| `Database:Seed`                   | `true`                 | popula usuários de exemplo se vazio    |
| `Seed:Password`                   | `CyberProtech@2026`    | senha dos usuários de exemplo           |
| `Jwt:Issuer` / `Jwt:Audience`     | `cyberprotech-api` / `cyberprotech-web` | validação do token        |
| `Jwt:SigningKey`                  | chave de laboratório   | assinatura HS256; mínimo 32 caracteres  |
| `Jwt:ExpirationMinutes`           | `60`                   | validade do token                       |
| `Frontend:BaseUrl`                | `http://localhost:5173` | origem liberada no CORS               |


## Portas (configuráveis via `.env`)

| Serviço  | Interna | Publicada (padrão) |
|----------|---------|--------------------|
| frontend | 80      | 5173               |
| backend  | 8080    | 5000               |
| db       | 5432    | (não publicada)    |

## Perfis do Compose

- Padrão (`docker compose up --build`): `db` + `backend` + `frontend`.
- Sob demanda: `toolkit` (perfil `toolkit`, executa e sai com o relatório).
