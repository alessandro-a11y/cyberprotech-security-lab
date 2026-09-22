# Guia de laboratório (uso controlado)

> Todo o uso é restrito ao ambiente Docker deste repositório
> (`localhost` / rede interna do Compose). Proibido usar contra sistemas externos.

## Cenários planejados (implementação na Fase 3)

1. **SQL Injection** — endpoint de busca com consulta vulnerável controlada + versão corrigida.
2. **XSS** — campo de comentário/perfil sem sanitização + versão com encoding/sanitização.
3. **IDOR / Broken Access Control** — leitura de recurso por ID sem checagem de dono/papel + correção.
4. **Falhas de autenticação** — login com mensagens genéricas, bloqueio/limite e política de senha (antes/depois).
5. **Security Misconfiguration** — Swagger/headers/versões expostas em dev vs. configuração segura.
6. **Exposição de informações** — mensagens de erro e payloads verbosos vs. respostas genéricas.

## Regras do laboratório

- Suba apenas via `docker compose up --build` (Docker Desktop ou Docker/WSL 2).
- Não aponte o toolkit nem os cenários para hosts externos.
- Não commitar senhas, tokens ou dumps reais (`docs/` contém apenas exemplos fictícios).
- Cada vulnerabilidade terá chave liga/desliga para alternar entre "vulnerável" e "corrigido" (Fase 6).
