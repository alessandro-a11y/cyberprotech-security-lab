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
backend/API            -> Controllers, Program.cs (HTTP, CORS, Swagger)
backend/Application    -> DTOs + Interfaces (contratos, sem dependência de infra)
backend/Domain         -> Entidades (ex.: User)
backend/Infrastructure -> EF Core + PostgreSQL (AppDbContext, repositórios, DI)
```

Dependências apontam para dentro: `API -> Application, Infrastructure`;
`Infrastructure -> Application, Domain`. `Domain` não depende de ninguém.

## Portas (configuráveis via `.env`)

| Serviço  | Interna | Publicada (padrão) |
|----------|---------|--------------------|
| frontend | 80      | 5173               |
| backend  | 8080    | 5000               |
| db       | 5432    | (não publicada)    |

## Perfis do Compose

- Padrão (`docker compose up --build`): `db` + `backend` + `frontend`.
- Sob demanda: `toolkit` (perfil `toolkit`, executa e sai com o relatório).
