# CyberProtech Web Security Lab + Security Toolkit

## 2. Descrição

Aplicação web de laboratório **propositalmente vulnerável em cenários controlados**,
acompanhada de um **Security Toolkit próprio** (Python) para análise de segurança.
O projeto existe para que estudantes pratiquem identificação e correção de falhas
web clássicas sem risco a sistemas reais.

**Estado atual (Fase 1 — Arquitetura):** somente a estrutura base foi criada
(backend, frontend, banco via Docker, toolkit esqueleto, CI e documentação).
Nenhuma vulnerabilidade foi implementada nesta etapa.

## 3. Objetivo

- Fornecer um ambiente de estudo **reprodutível via Docker Compose**.
- Praticar vulnerabilidades web comuns em ambiente isolado.
- Desenvolver um toolkit de análise (headers, cookies, endpoints, configurações, relatórios).
- Documentar o ciclo completo: explorar → corrigir → testar.

## 4. ⚠️ Aviso de segurança — uso exclusivamente em ambiente controlado

- Execute **somente** com `docker compose up --build` neste repositório
  (serviços locais e rede interna do Compose).
- **Nunca** aponte o toolkit, os cenários de laboratório ou qualquer script
  para sistemas externos, redes de terceiros ou ambientes de produção.
- As vulnerabilidades (a partir da Fase 3) são **propositalmente didáticas** e
  jamais devem ser replicadas fora do laboratório.
- Não commitar senhas, tokens, API keys ou connection strings reais
  (use `.env` local a partir do `.env.example`).

## 5. Arquitetura

```text
frontend (React + Vite, nginx) ──HTTP /api/*──> backend (ASP.NET Core 8)
        │                                              │
        │ relatórios do toolkit (Fase 4)               │ EF Core (Npgsql)
        v                                              v
toolkit (Python, perfil sob demanda)          PostgreSQL (volume nomeado pgdata)
```

- `backend/API` — Controllers, endpoints de health, handler global de erros,
  `Program.cs` (CORS, Swagger, DI).
- `backend/Application` — DTOs e interfaces (contratos).
- `backend/Domain` — entidades (ex.: `User`).
- `backend/Infrastructure` — EF Core + PostgreSQL (`AppDbContext`, migrations,
  repositórios, seed).
- `frontend/src` — `pages/` (Login, Dashboard, Usuários, Laboratório, Toolkit),
  `api/client.js`, `components/Layout.jsx`.
- `toolkit/src/toolkit` — `scanner`, `headers`, `cookies`, `endpoints`,
  `config_checks`, `report`, `cli`.
- Detalhes em [`docs/architecture.md`](docs/architecture.md).

## 6. Tecnologias

| Camada    | Tecnologia                          |
|-----------|-------------------------------------|
| Backend   | ASP.NET Core 8, EF Core, Npgsql     |
| Frontend  | React 18, React Router 6, Vite 5    |
| Banco     | PostgreSQL 16 (Docker)              |
| Toolkit   | Python 3.12                         |
| Infra     | Docker / Docker Compose             |
| CI        | GitHub Actions                      |

## 7. Estrutura de pastas

```text
cyberprotech-security-lab/
├── backend/            # API / Application / Domain / Infrastructure (ver backend/README.md)
├── frontend/           # React + Vite (Dockerfile + nginx.conf)
├── toolkit/            # Security Toolkit (Python)
├── docs/               # architecture.md, lab-guide.md, toolkit.md
├── docker/             # backend.Dockerfile, toolkit.Dockerfile
├── tests/              # reservado à Fase 5 (ver tests/README.md)
├── .github/workflows/  # ci.yml
├── .env.example        # variáveis de exemplo (sem segredos)
├── .gitignore
├── docker-compose.yml
└── README.md
```

O que existe no backend até a Fase 3: arquitetura em quatro camadas,
`User`, `AppDbContext` com migration inicial, seed dos usuários de exemplo,
health check de liveness e readiness, tratamento global de erros, CORS,
autenticação com BCrypt + JWT, papéis `Admin`/`User` com policies de
autorização, rate limit no login, perfil do usuário (editar e-mail/bio e trocar
senha) e a área administrativa (`/api/admin/stats` e troca de papel). O frontend
segue com 5 telas usando dados de exemplo; o toolkit, com CLI e módulos em
esqueleto; o Compose, com `db`, `backend`, `frontend` e `toolkit` (sob demanda).

## 8. Funcionalidades planejadas

- Login e gestão de usuários — **API pronta na Fase 2**; falta ligar o frontend
  a `POST /api/auth/login` (Sprint 4).
- Dashboard com status da API/banco e últimos relatórios do toolkit (Fase 4).
- Laboratório com cenários vulneráveis alternáveis vulnerável/corrigido (Fases 3 e 6).
- Tela de resultados do Security Toolkit (Fase 4).
- Suite de testes automatizados (Fase 5).

## 9. Vulnerabilidades planejadas (somente a partir da Fase 3)

1. **SQL Injection**
2. **XSS (Cross-Site Scripting)**
3. **IDOR / Broken Access Control**
4. **Falhas de autenticação**
5. **Security Misconfiguration**
6. **Exposição de informações**

Regras e escopo controlado em [`docs/lab-guide.md`](docs/lab-guide.md).

## 10. Security Toolkit

Ferramenta própria (Python) para análises **passivas** no ambiente do laboratório:

- análise de headers HTTP;
- análise de cookies;
- análise de endpoints;
- verificações de configuração;
- geração de relatórios (JSON/Markdown).

Uso base (detalhes em [`docs/toolkit.md`](docs/toolkit.md)):

```bash
pip install -r toolkit/requirements.txt
PYTHONPATH=toolkit/src python -m toolkit.cli --target http://localhost:5000 --out report.json

# Via Docker (rede interna do Compose):
docker compose --profile toolkit run --rm toolkit --target http://backend:8080
```

## 11. Pré-requisitos

- **Docker Desktop** (Windows) ou **Docker Engine + Compose v2** (Linux/WSL 2).
- Git.
- (Opcional, sem Docker) .NET 8 SDK, Node 20+, Python 3.11+.

## 12. Execução com Docker Desktop (para a equipe — sem WSL)

1. Instale o **Docker Desktop** e ative o backend WSL 2 dele (padrão).
2. Clone o repositório e entre na pasta:
   ```bash
   git clone <URL-DO-REPOSITORIO-PUBLICO>
   cd cyberprotech-security-lab
   ```
3. (Opcional) crie seu `.env` local:
   ```bash
   cp .env.example .env
   ```
4. Suba tudo:
   ```bash
   docker compose up --build
   ```
5. Acesse:
   - Frontend: <http://localhost:5173>
   - API / health: <http://localhost:5000/api/health>
   - Swagger (dev): <http://localhost:5000/swagger>
6. Para encerrar:
   ```bash
   docker compose down
   ```
   Para encerrar e apagar o volume do banco (recomeçar do zero):
   ```bash
   docker compose down -v
   ```

## 13. Execução com Docker/WSL 2

Os mesmos comandos da seção anterior funcionam dentro do WSL 2
(Ubuntu com Docker Engine + plugin Compose). Nenhuma configuração
específica de WSL foi criada: o Compose usa apenas variáveis de ambiente
e volumes nomeados, portanto é portátil entre Docker Desktop e WSL 2.

```bash
docker compose up --build
docker compose down
```

## 14. Como contribuir

1. Abra uma issue ou escolha uma tarefa do roadmap.
2. Crie um branch a partir da `main`: `git checkout -b feat/minha-tarefa`.
3. Commits pequenos e descritivos (ex.: `feat(frontend): tela de login base`).
4. Garanta `docker compose up --build` funcionando antes do PR.
5. Nunca commitar `.env`, senhas, tokens ou dumps. A CI valida build do
   backend, build do frontend e compilação do toolkit.

## 15. Roadmap

- **Fase 1 — Arquitetura** (atual): estrutura base, Compose, CI, docs. ✅
- **Fase 2 — Aplicação**: autenticação, usuários, migrations, telas funcionais.
- **Fase 3 — Vulnerabilidades controladas**: SQLi, XSS, IDOR, auth, misconfig, info exposure.
- **Fase 4 — Security Toolkit**: análises reais + relatórios.
- **Fase 5 — Testes**: xUnit, smoke do frontend, pytest do toolkit.
- **Fase 6 — Correções**: versões seguras + chaves vulnerável/corrigido.
- **Fase 7 — Documentação**: guias finais, evidências, hardening.
- **Fase 8 — Apresentação**: roteiro e demonstração.

## 16. Organização da equipe (5 integrantes)

Sugestão de divisão (ajustar nomes no repositório/quadro):

| Papel | Foco |
|-------|------|
| Backend (2) | API, EF Core/migrations, autenticação/autorização |
| Frontend (1) | Telas, consumo da API, resultados do toolkit |
| Toolkit + Testes (1) | Análises Python, relatórios, pytest |
| DevOps + Docs (1) | Compose, CI, README/docs, apresentação |

---

*Projeto educacional. Qualquer uso fora do ambiente controlado deste
laboratório é proibido.*
