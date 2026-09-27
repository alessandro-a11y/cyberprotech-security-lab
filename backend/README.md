# Backend — CyberProtech Web Security Lab

API em ASP.NET Core 8 com EF Core e PostgreSQL. Consulte `../docs/architecture.md`
para o desenho geral do laboratório.

## Estrutura

```text
backend/
├── API/            -> HTTP: controllers, endpoints de health, handler de erros, DI, Swagger
├── Application/    -> DTOs e interfaces (contratos; não depende de Infrastructure)
├── Domain/         -> entidades
└── Infrastructure/ -> EF Core + Npgsql, segurança (BCrypt, JWT), seed
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

## Usuários de exemplo

Todos compartilham a senha definida em `Seed:Password`
(`CyberProtech@2026` por padrão).

| Usuário      | Papel |
|--------------|-------|
| `admin`      | Admin |
| `carla.admin`| Admin |
| `aluno01`    | User  |
| `maria.souza`| User  |
| `joao.lima`  | User  |
| `pedro.alves`| User  |

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

## Endpoints

| Método | Rota                    | Acesso     | Descrição                          |
|--------|-------------------------|------------|------------------------------------|
| POST   | `/api/auth/register`    | público    | Cadastra e devolve token           |
| POST   | `/api/auth/login`       | público    | Autentica e devolve token          |
| GET    | `/api/auth/me`          | logado     | Usuário do token                   |
| GET    | `/api/users`            | logado     | Lista / busca                      |
| GET    | `/api/users/{id}`       | dono/Admin | Um usuário                         |
| PUT    | `/api/users/{id}`       | Admin      | Atualiza a bio                     |
| DELETE | `/api/users/{id}`       | Admin      | Remove                             |
| GET    | `/api/health`           | público    | Liveness (não toca no banco)       |
| GET    | `/api/health/ready`     | público    | Readiness (503 se o banco falhar)  |

O token vai no header: `Authorization: Bearer <token>`.

## Autenticação

- **Senha:** BCrypt custo 12, com sal aleatório por senha. O custo está em
  `Infrastructure/Security/BcryptPasswordHasher.cs`; o contrato
  `IPasswordHasher` existe para permitir a troca por Argon2id sem tocar nos
  controllers.
- **JWT:** HS256, validando emissor, audiência, assinatura e validade
  (`Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes`).
- **Papéis:** `Admin` e `User`, em policies nomeadas
  (`API/Infrastructure/AuthorizationPolicies.cs`).

### Configuração de produção

| Chave                     | Como definir                | Observação                          |
|---------------------------|-----------------------------|-------------------------------------|
| `Jwt:SigningKey`          | `Jwt__SigningKey`           | Mínimo 32 caracteres; a API não sobe com chave curta ou vazia |
| `Seed:Password`           | `Seed__Password`            | Senha dos usuários de exemplo      |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Banco |

A chave e a senha do `.env.example` são de laboratório. Gere uma chave por
ambiente com `openssl rand -base64 48`.

## Decisões de segurança desta fase

O que já está **correto** aqui — e que a Fase 3 vai desfazer de propósito para
virar laboratório, e a Fase 5 vai consertar:

- o auto-cadastro sempre cria `User`; o corpo da requisição não escolhe o papel
  (enviar `role: "Admin"` no cadastro é ignorado);
- `PUT /api/users/{id}` não aceita `role` nem senha, então ninguém se promove
  por ali;
- login responde sempre a mesma mensagem, e o hash é conferido mesmo quando o
  usuário não existe, para não vazar existência de conta pelo tempo de resposta;
- `GET /api/users/{id}` exige ser o dono ou Admin;
- o token é validado por assinatura — editar o payload (por exemplo trocar
  `User` por `Admin`) devolve 401;
- `POST /api/auth/login` tem rate limit de 10 tentativas por minuto por IP,
  devolvendo 429;
- `UserDto` nunca expõe `PasswordHash`; erros voltam como ProblemDetails sem
  stack trace nem SQL.

## Integração com o frontend

Com a API no ar, desligue os dados de exemplo do frontend:

```
VITE_API_BASE_URL=http://localhost:5000
VITE_USE_SAMPLE_DATA=false
```

Os Ids e as datas do seed acompanham `frontend/src/data/sampleUsers.js`, então
a troca não quebra as telas. O `Login.jsx` ainda usa `sampleApi.findByUsername`
local: a integração com `POST /api/auth/login` e o armazenamento do token
(`frontend/src/session.js`) é o próximo passo da Sprint 4.
