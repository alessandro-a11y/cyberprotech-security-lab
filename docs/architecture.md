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
backend/API            -> Controllers, Endpoints (health), Handlers (erros, policies, CORS), Program.cs, Swagger
backend/Application    -> DTOs + Interfaces (contratos, sem dependência de infra)
backend/Domain         -> Entidades (ex.: User)
backend/Infrastructure -> EF Core + PostgreSQL (AppDbContext, Migrations, repositórios, seed)
                         + Security (BCrypt, JWT)
                         + Lab (LabState, controles do laboratório)
```

Dependências apontam para dentro: `API -> Application, Infrastructure`;
`Infrastructure -> Application, Domain`. `Domain` não depende de ninguém.

Detalhes de setup, comandos de migration e a tabela de endpoints estão em
`backend/README.md`. O contrato que o frontend consome está em
`docs/integration.md`.

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
- Todos os usuários do seed compartilham a senha de `Seed:Password` (obrigatória,
  vem do `.env`), gravada com BCrypt.

## Autenticação e autorização

```text
POST /api/auth/register         -> cria User e devolve token
POST /api/auth/login            -> confere BCrypt e devolve token
GET  /api/auth/me               -> usuário do token
PUT  /api/auth/me               -> e-mail e bio próprios
POST /api/auth/change-password  -> exige a senha atual

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

`AdminController` aplica `AdminOnly` na classe inteira — não há
`[AllowAnonymous]` em nenhum método, e é para lá que a Fase 3 vai olhar ao
construir o cenário de Broken Access Control.

### Travas de papel

Toda alteração de papel passa por três checagens, nesta ordem:

1. o papel precisa estar na allowlist `Admin`/`User` — valor arbitrário do
   cliente criaria um papel que nenhuma policy reconhece (`400`);
2. o Admin não altera o próprio papel (`409`);
3. o último Admin não é rebaixado nem removido (`409`).

A ordem importa: como a trava 2 vem antes da 3, a 3 é defesa em profundidade e
hoje inalcançável pela API. Fica registrada em
`Known limitations` do `backend/README.md`.

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
| `Seed:Password`                   | obrigatória (do `.env`) | senha dos usuários de exemplo           |
| `Database:MaxPoolSize`            | `20`                   | conexões por processo; réplicas × pool < `max_connections` |
| `Lab:Enabled`                     | `false`                | interruptor mestre do laboratório       |
| `Lab:VulnMode`                    | `false`                | estado inicial do modo vulnerável      |
| `Lab:VerboseErrors`               | `false`                | estado inicial dos erros detalhados    |
| `Lab:RateLimit`                   | `true`                 | estado inicial do limite de login      |
| `Lab:SecurityHeaders`             | `true`                 | estado inicial dos headers              |
| `Jwt:Issuer` / `Jwt:Audience`     | `cyberprotech-api` / `cyberprotech-web` | validação do token        |
| `Jwt:SigningKey`                  | chave de laboratório   | assinatura HS256; mínimo 32 bytes        |
| `Jwt:ExpirationMinutes`           | `60`                   | validade do token                       |
| `Frontend:BaseUrl`                | `http://localhost:5173` | origem do CORS; aceita `;` ou `,` |

## Controles do laboratório

```text
GET /api/lab/config  -> estado dos 4 controles (logado)
PUT /api/lab/config  -> altera os enviados (Admin)
```

`LabState` (singleton, em memória) é a única fonte do estado. Gravar exige
`Lab:Enabled=true` **e** ambiente `Development`; sem os dois, `409`. O padrão
`Lab:Enabled=false` em `appsettings.json` faz um deploy sem configuração ficar
seguro por construção, não por disciplina.

Os `id` dos controles são os que `frontend/src/pages/Admin.jsx` já usa, para o
frontend não precisar traduzir.

| Controle          | Ancorado em (Sprint 5)                        |
|-------------------|----------------------------------------------|
| `vuln-mode`       | SQLi em `SearchAsync`, IDOR em `GetById`, bio sem `BioSanitizer` |
| `verbose-errors`  | `GlobalExceptionHandler`                      |
| `rate-limit`      | factory da policy `login`                     |
| `sec-headers`     | `SecurityHeadersMiddleware`                   |

Os quatro estão ancorados. Detalhes de exploração, payloads e a matriz de
verificação estão em `docs/lab-guide.md`.

Duas peculiaridades de implementação que valem registro:

- **O estado entra na chave de partição do rate limiter.** `RateLimitPartition`
  memoiza o limitador por chave, então a factory só roda na primeira
  requisição. Sem o prefixo, virar o toggle não re-avaliava nada.
- **O sanitizador de bio é segunda camada, não correção.** Escapar na
  renderização continua sendo responsabilidade do frontend. Ver
  `docs/lab-guide.md`.



## Portas (configuráveis via `.env`)

| Serviço  | Interna | Publicada (padrão) |
|----------|---------|--------------------|
| frontend | 80      | 5173               |
| backend  | 8080    | 5000               |
| db       | 5432    | (não publicada)    |

## Perfis do Compose

- Padrão (`docker compose up --build`): `db` + `backend` + `frontend`.
- Sob demanda: `toolkit` (perfil `toolkit`, executa e sai com o relatório).
