# Backend — CyberProtech Web Security Lab

API em ASP.NET Core 8 com EF Core e PostgreSQL. Consulte `../docs/architecture.md`
para o desenho geral do laboratório.

## Estrutura

```text
backend/
├── API/            -> HTTP: controllers, endpoints de health, middleware, DI, Swagger
├── Application/    -> DTOs e interfaces (contratos; não depende de Infrastructure)
├── Domain/         -> entidades
└── Infrastructure/ -> EF Core + Npgsql: AppDbContext, migrations, repositórios, seed
```

Dependências apontam para dentro: `API -> Application, Infrastructure` e
`Infrastructure -> Application, Domain`. `Domain` não depende de ninguém.

## Subir o ambiente

Com Docker (recomendado, sobe banco + API + frontend):

```bash
docker compose up --build
```

Só a API, com o Postgres do compose: suba o `db` e rode a API localmente
(ajuste `ConnectionStrings__DefaultConnection` para `Host=localhost`).

Sem Docker, com um PostgreSQL local na porta 5432:

```powershell
dotnet run --project API
```

As migrations pendentes são aplicadas na inicialização e o banco é populado
com seis usuários de exemplo. Para desligar esse comportamento:

```json
"Database": { "MigrateOnStartup": false, "Seed": false }
```

## Migrations

O `dotnet-ef` está fixado no manifesto local (`backend/.config/dotnet-tools.json`),
na versão 8.0.11 — assim todos usam a mesma versão da aplicação. Restaure uma vez
após clonar:

```bash
cd backend
dotnet tool restore
```

Comandos executados de dentro de `backend/`:

```bash
# criar migration
dotnet ef migrations add <Nome> --project Infrastructure --startup-project API --output-dir Persistence/Migrations

# aplicar no banco
dotnet ef database update --project Infrastructure --startup-project API

# reverter a última migration
dotnet ef migrations remove --project Infrastructure --startup-project API

# gerar SQL sem tocar no banco (útil em revisão de código)
dotnet ef migrations script --project Infrastructure --startup-project API --idempotent
```

A `AppDbContextFactory` (design-time) constrói o contexto sem subir a API,
então nenhum desses comandos precisa de conexão válida para *gerar* código.

## Endpoints (Fase 1)

| Método | Rota                    | Descrição                                  |
|--------|-------------------------|--------------------------------------------|
| GET    | `/api/health`            | Liveness. Não toca no banco.               |
| GET    | `/api/health/ready`      | Readiness. 503 se o PostgreSQL falhar.     |
| GET    | `/api/users`             | Lista usuários.                            |
| GET    | `/api/users?search=`     | Filtra por usuário ou e-mail.              |
| GET    | `/api/users/{id:guid}`   | Um usuário. 404 se não existir.            |

Sem autenticação nesta fase — isso chega na `Fase 2`. `UserDto` nunca expõe
o `PasswordHash`, e erros voltam como ProblemDetails sem stack trace.

## Integração com o frontend

Com a API no ar, desligue os dados de exemplo do frontend:

```
VITE_API_BASE_URL=http://localhost:5000
VITE_USE_SAMPLE_DATA=false
```

Os Ids e as datas do seed acompanham `frontend/src/data/sampleUsers.js`, então
a troca não quebra as telas.
