# Pasta reservada aos testes automatizados (implementação na Fase 5).

Planejado:
- `tests/backend/` — testes xUnit da API (autenticação, autorização, cenários corrigidos).
- `tests/frontend/` — testes de build/smoke das telas.
- `tests/toolkit/` — testes pytest das análises (headers, cookies, endpoints, config).

A CI (`.github/workflows/ci.yml`) já executa `dotnet test` quando houver projetos
de teste e compila o toolkit a cada push/PR.
