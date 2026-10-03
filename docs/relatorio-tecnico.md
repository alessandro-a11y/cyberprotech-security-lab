# Relatório técnico — CyberProtech Web Security Lab

## 1. Objetivo e escopo

O CyberProtech Web Security Lab é um **laboratório controlado** de segurança
web. A aplicação — uma API REST e um portal web — é **propositalmente
vulnerável** em cenários inspirados na classificação OWASP Top 10, cada um
ativável em tempo de execução por um flag dedicado.

O projeto existe para que estudantes pratiquem o ciclo completo de segurança
ofensiva e defensiva em cinco etapas:

1. **Desenvolver** — construir a aplicação com as falhas embutidas.
2. **Explorar** — provocar cada vulnerabilidade no estado vulnerável.
3. **Analisar** — usar a ferramenta própria (Security Toolkit) para detectar
   os problemas de forma automatizada.
4. **Corrigir** — alternar o controle para o estado seguro e confirmar que o
   comportamento muda.
5. **Validar** — reexecutar a ferramenta e verificar que os achados
   desaparecem.

**Nenhum cenário é acessível sem flag.** Gravar uma alteração nos controles
exige simultaneamente `Lab:Enabled=true` e `ASPNETCORE_ENVIRONMENT=Development`;
fora dessas condições, `PUT /api/lab/config` responde `409`. O padrão do
`appsettings.json` é `Lab:Enabled=false`: um deploy sem configuração fica
seguro por construção, não por disciplina.

A stack compreende API .NET 8 / EF Core / PostgreSQL 16, portal React + Vite
servido por nginx, toolkit de análise em Python, e Docker Compose reunindo os
três serviços. São 15 endpoints, 6 telas e 6 cenários com toggle.

Cobertura de testes: **175 testes passando** (71 unitários backend + 37
integração backend + 59 frontend + 8 toolkit).

## 2. Arquitetura

A arquitetura completa, com diagramas e detalhes de cada camada, está em
[`docs/architecture.md`](architecture.md). Segue o resumo.

```text
            +-----------------+
            |    frontend     |  React + Vite (nginx em produção)
            | localhost:5173  +------+
            +--------+--------+      |
                     | HTTP /api/*   |
                     v               |  relatórios JSON
            +--------+--------+      |
            |     backend     |      |
            | ASP.NET Core 8  +------+
            | localhost:5000  |      |
            +--------+--------+      |
                     | EF Core       |
                     v               |
            +--------+--------+      |
            |   PostgreSQL 16 |      |
            |  volume pgdata  |      |
            +-----------------+      |
                                     v
                            +--------+--------+
                            |     toolkit     |  Python (sob demanda)
                            +-----------------+
```

### Camadas do backend

| Camada         | Responsabilidade                                           |
|----------------|------------------------------------------------------------|
| `backend/API`  | Controllers, endpoints de health, handler global de erros, CORS, Swagger, DI |
| `backend/Application` | DTOs e interfaces (contratos, sem dependência de infra) |
| `backend/Domain`      | Entidades (`User`)                                     |
| `backend/Infrastructure` | EF Core + PostgreSQL, BCrypt, JWT, `LabState`, seed |

Dependências apontam para dentro: `API → Application, Infrastructure`;
`Infrastructure → Application, Domain`. `Domain` não depende de ninguém.

O EF Core aplica migrations automaticamente na inicialização e popula o seed
de 6 usuários se a tabela `users` estiver vazia. O contrato de integração que
o frontend consome está em [`docs/integration.md`](integration.md).

O Docker Compose define três serviços permanentes (`db`, `backend`, `frontend`)
e o `toolkit` como perfil sob demanda.

## 3. Cenários de vulnerabilidade

Cada cenário é demonstrável nos dois estados — vulnerável e corrigido —
alternando o controle correspondente na tela de Administração ou via
`PUT /api/lab/config`. As evidências abaixo são de verificação real contra a
API e o banco.

### 3.1 SQL Injection

| Atributo      | Valor                                                    |
|---------------|----------------------------------------------------------|
| OWASP         | A03:2021 — Injection                                     |
| Severidade    | **Crítica**                                              |
| Toggle        | `vuln-mode`                                              |
| Demonstração  | Clicável na tela (campo de busca em Usuários) ou por terminal |

**Vulnerável:** o termo de busca `' OR '1'='1` é concatenado no SQL em
`EfUserRepository.SearchAsync`. Resultado: **6 linhas** (a tabela inteira de
usuários).

**Corrigido:** o mesmo termo viaja como parâmetro. Resultado: **0 linhas**.

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/users?search=%27%20OR%20%271%27%3D%271"
```

### 3.2 XSS armazenado

| Atributo      | Valor                                                    |
|---------------|----------------------------------------------------------|
| OWASP         | A03:2021 — Injection                                     |
| Severidade    | **Alta**                                                 |
| Toggle        | `vuln-mode`                                              |
| Demonstração  | Clicável na tela (editar bio no Perfil)                  |

O XSS tem duas camadas. A API, com o controle ligado, grava HTML cru no
PostgreSQL sem sanitizar (o `BioSanitizer` é desativado). O frontend renderiza
a bio com `dangerouslySetInnerHTML`, executando o payload no navegador. Este é
o único cenário demonstrável inteiramente na tela, sem terminal.

**Vulnerável:** payload `<script>alert(document.cookie)</script>` é gravado
intacto no banco e executado no navegador ao abrir o perfil.

**Corrigido:** `BioSanitizer` remove as tags na gravação; o conteúdo é
renderizado como texto plano.

```bash
curl -X PUT http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"email":"aluno01@cyberprotech.lab","bio":"<script>alert(1)</script>"}'
```

### 3.3 IDOR / Broken Access Control

| Atributo      | Valor                                                    |
|---------------|----------------------------------------------------------|
| OWASP         | A01:2021 — Broken Access Control                         |
| Severidade    | **Crítica**                                              |
| Toggle        | `vuln-mode`                                              |
| Demonstração  | Por terminal (`curl` com id alheio) ou via DevTools      |

**Vulnerável:** `GET /api/users/{id}` deixa de comparar o id da rota com o
`NameIdentifier` do token. Um User lê o perfil do Admin: **200** com
`admin@cyberprotech.lab`.

**Corrigido:** a comparação é reativada. Resultado: **403**, sem dados.

O cenário é exclusivamente de leitura: `AdminController` exige `AdminOnly`
sempre, em qualquer estado.

### 3.4 Configuração insegura (Security Headers)

| Atributo      | Valor                                                    |
|---------------|----------------------------------------------------------|
| OWASP         | A05:2021 — Security Misconfiguration                     |
| Severidade    | **Média**                                                |
| Toggle        | `sec-headers`                                            |
| Demonstração  | Clicável na tela (tela de Administração) ou por terminal |

**Vulnerável:** nenhum header de segurança é enviado (**0 cabeçalhos**).

**Corrigido:** **6 cabeçalhos** — `Content-Security-Policy`,
`Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`,
`Referrer-Policy` e `X-CyberProtech-Lab`.

```bash
curl -sI http://localhost:5000/api/health | head -12
```

### 3.5 Falhas de autenticação (Rate Limit)

| Atributo      | Valor                                                    |
|---------------|----------------------------------------------------------|
| OWASP         | A07:2021 — Identification and Authentication Failures    |
| Severidade    | **Alta**                                                 |
| Toggle        | `rate-limit`                                             |
| Demonstração  | Clicável na tela (errar login repetidamente) ou por terminal |

**Vulnerável:** 14 tentativas de senha errada, **nenhum 429**.

**Corrigido:** `401` nas primeiras **10** tentativas, depois **429 × 4** — o limite
é 10/min por IP, e o `429` começa na 11ª. Números medidos por
`scripts/verificar-cenarios.sh`, que espera a janela expirar antes de contar;
sem essa espera o `429` aparece antes, porque as tentativas do teste anterior já
consomiram parte do orçamento.

```bash
for i in $(seq 1 14); do
  curl -o /dev/null -w "%{http_code} " -X POST http://localhost:5000/api/auth/login \
    -H 'Content-Type: application/json' -d '{"username":"admin","password":"errada"}'
done
```

### 3.6 Exposição de informações (Verbose Errors)

| Atributo      | Valor                                                    |
|---------------|----------------------------------------------------------|
| OWASP         | A02:2021 — Cryptographic Failures / Information Exposure |
| Severidade    | **Média**                                                |
| Toggle        | `verbose-errors`                                         |
| Demonstração  | **Somente por linha de comando** (ver explicação abaixo) |

**Vulnerável:** a resposta do erro inclui `exception`, `stackTrace` e
`sqlState` — incluindo a mensagem do PostgreSQL com o SQL que o banco recusou.

**Corrigido:** corpo genérico com `title`, `status`, `detail`, `instance` e
`traceId`, sem informação interna.

A demonstração exige duas requisições de cadastro idênticas em paralelo: a
primeira cria o usuário e a segunda provoca a violação de unicidade que o
handler global converte em resposta detalhada.

```bash
payload='{"username":"probe_verbose","email":"probe_verbose@lab.invalid","password":"Toolkit1!"}'
for i in 1 2; do
  curl -sS -X POST http://localhost:5000/api/auth/register \
    -H 'Content-Type: application/json' -d "$payload" &
done
wait
```

**Por que não é demonstrável pela tela:** o cadastro no portal trata e-mail
existente antes de alcançar a violação de unicidade no banco, devolvendo o
mesmo `409` nos dois estados. A sonda do toolkit provoca a condição porque
precisa; um humano não clica em duas requisições ao mesmo tempo. Este é o
gancho de honestidade técnica do projeto — nem todo cenário se presta a
demonstração visual.

## 4. Security Toolkit

O Security Toolkit é uma ferramenta própria em Python que executa verificações
HTTP contra o laboratório. A documentação operacional está em
[`docs/toolkit.md`](toolkit.md).

### Como funciona a varredura

O orquestrador (`scanner.py`) executa 4 módulos de análise em sequência e
agrega os achados em um `ScanResult`. A saída pode ser JSON ou Markdown.

### Módulos

| Módulo       | Arquivo               | Tipo        | O que verifica                              |
|--------------|-----------------------|-------------|---------------------------------------------|
| Headers HTTP | `headers.py`          | **Passivo** | CSP, HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy |
| Sessão       | `cookies.py`          | **Passivo** | Risco do Bearer em localStorage vs. cookie HttpOnly |
| Endpoints    | `endpoints.py`        | **Passivo** | Swagger e readiness públicos                |
| Configuração | `config_checks.py`    | **Ativo**   | Confirmação do middleware de headers e stack trace exposto |
| Relatórios   | `report.py`           | —           | Geração de JSON e Markdown                  |

A única sonda ativa é um par de cadastros idênticos no módulo de configuração,
que provoca a condição de erro necessária para verificar se `verbose-errors`
expõe stack trace. Nenhum payload de exploração é enviado.

### Resultado da validação

| Estado       | Achados |
|--------------|---------|
| Corrigido    | **4**   |
| Vulnerável   | **9**   |

Diferença entre os estados — **7 achados que desaparecem ao corrigir:**

1. CSP ausente (header)
2. HSTS ausente (header)
3. X-Content-Type-Options ausente (header)
4. X-Frame-Options ausente (header)
5. Referrer-Policy ausente (header)
6. Headers de segurança não confirmados (config)
7. Stack trace exposto (config)

**2 achados que permanecem no estado corrigido:**

1. CSP permite `unsafe-inline` em `script-src` — **high** (ver Limitações)
2. CSP permite `unsafe-inline` em `style-src` — **low**

### Ressalva operacional

A sonda `verbose-errors` deixa um usuário `scan_*` no banco a cada execução.
Para limpar:

```sql
DELETE FROM users WHERE "Username" LIKE 'scan\_%';
```

## 5. Métricas de carga

Os resultados detalhados, a metodologia e a análise estão em
[`docs/load-test.md`](load-test.md). A tabela canônica foi medida em WSL2,
container único, PostgreSQL 16 em container, com **2006 usuários**.

| Cenário | Workers | Vazão | p50 | p95 | p99 |
|---|---|---|---|---|---|
| `health` (sem banco) | 50 | 9.551 req/s | 3,6 ms | 12,8 ms | 23,3 ms |
| `users` | 10 | 874 req/s | 8,9 ms | 22,1 ms | 39,0 ms |
| `users` | 50 | 1.351 req/s | 31,0 ms | 66,7 ms | 100,7 ms |
| `users` | 100 | ~1.070 req/s | 66–93 ms | — | — |
| `users` | 400 | ~720 req/s | 463–608 ms | 710 ms | 796 ms |
| `users-busca` | 50 | 507 req/s | 83,4 ms | 182,9 ms | 252,5 ms |
| `user-por-id` | 100 | 1.591 req/s | 50,4 ms | 121,1 ms | 187,8 ms |
| `stats` | 50 | 810 req/s | 54,1 ms | 100,2 ms | 125,0 ms |
| `login` (BCrypt 12) | 1 | 2,1 req/s | 467,9 ms | 544,5 ms | 596,0 ms |
| `login` | 10 | 8,3 req/s | 1.098 ms | 1.440 ms | 1.606 ms |

### Variância assumida

A variância medida nesta máquina vai de **~10% a ~40%**, porque a VM do WSL2
disputa CPU com o Docker e com o próprio host. Leitura única não é confiável
neste ambiente.

### Leitura dos resultados

- **O gargalo é o framework, não o SQL.** `health`, que não toca no banco,
  faz ~9.500 req/s; `users`, que vai ao banco com índice, faz ~1.350. A
  diferença entre leituras com banco (`users` vs. `user-por-id`) é pequena:
  o custo está no caminho HTTP, não no SQL.
- **O login a 2,1 req/s é o BCrypt custo 12 deliberado, não um defeito.** O
  hash ocupa o núcleo inteiro por design. Com o rate limit ligado, o limiter
  responde antes de gastar BCrypt, que é exatamente o que se quer.
- **A busca é o caminho mais lento** (507 req/s). `ILIKE '%termo%'` não usa
  índice (`Seq Scan`); com volume alto, `pg_trgm` + GIN seria a solução.

## 6. Limitações conhecidas

As seguintes limitações são reais e documentadas de propósito:

1. **A CSP mantém `unsafe-inline` em `script-src`.** O CSS inline que o Vite
   injeta no bundle quebraria sem isso. O scanner acusa com razão — é o achado
   `high` que sobrevive no estado corrigido. A remoção exige validar que o
   bundle do Vite continua funcionando sem o `unsafe-inline`, o que não foi
   feito até agora.

2. **O token Bearer fica em `localStorage`, legível por XSS.** Qualquer
   script que execute no contexto da página pode ler o token. Um cookie
   `HttpOnly` reduziria o risco, mas mudaria o fluxo de autenticação. O
   módulo de sessão do toolkit registra este achado como `info`.

3. **O verbose-errors não é demonstrável pela tela.** O cadastro trata e-mail
   existente antes da violação de unicidade e devolve o mesmo `409` nos dois
   estados. A sonda do toolkit provoca a condição porque precisa; um humano
   não clica em duas requisições ao mesmo tempo. Este é o cenário onde a
   honestidade técnica do projeto se manifesta: nem toda falha é clicável.

4. **Bio gravada no modo vulnerável não é retro-sanitizada.** Voltar o toggle
   para o estado corrigido não limpa o que já está no banco. É preciso
   reescrever o campo manualmente. O comportamento é proposital e realista:
   sanitizar na escrita não corrige o que já foi contaminado.

5. **O cenário de IDOR é só na leitura.** `AdminController` exige `AdminOnly`
   na classe inteira, sem `[AllowAnonymous]` em nenhum método. A escrita
   administrativa é protegida em qualquer estado.

## 7. Como reproduzir

### Do clone ao relatório

```bash
git clone <URL-DO-REPOSITÓRIO>
cd cyberprotech-security-lab

# 1. Criar o .env a partir do exemplo
cp .env.example .env

# 2. Gerar os segredos obrigatórios (os três são exigidos pelo Compose com :?)
#    JWT_SIGNING_KEY — mínimo 32 caracteres; a API não sobe com chave curta
openssl rand -base64 48    # → colar em JWT_SIGNING_KEY
#    POSTGRES_PASSWORD — senha do banco
openssl rand -base64 24    # → colar em POSTGRES_PASSWORD
#    SEED_PASSWORD — a senha que você usará para logar no portal
#    Escolha uma manualmente e cole em SEED_PASSWORD

# 3. Se a porta 5432 do host estiver ocupada, trocar DB_PORT para 5434
#    (o .env.example já documenta isso)

# 4. Subir o ambiente
docker compose up -d --build
```

### Portas

| Serviço    | Porta |
|------------|-------|
| Portal     | 5173  |
| API        | 5000  |
| PostgreSQL | 5434 (configurável via `DB_PORT`) |

O Swagger está disponível em `http://localhost:5000/swagger` quando
`ASPNETCORE_ENVIRONMENT=Development` (padrão no Compose).

### Autenticar e testar

```bash
SEED_PASSWORD=$(grep '^SEED_PASSWORD=' .env | cut -d= -f2-)
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d "{\"username\":\"admin\",\"password\":\"$SEED_PASSWORD\"}" \
  | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
```

### Alternar estados

```bash
# Ligar o modo vulnerável
curl -X PUT http://localhost:5000/api/lab/config \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"vulnMode":true}'

# Voltar ao estado seguro
curl -X PUT http://localhost:5000/api/lab/config \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"vulnMode":false,"verboseErrors":false,"rateLimit":true,"securityHeaders":true}'
```

### Executar o Security Toolkit

```bash
docker compose --profile toolkit run --rm toolkit --target http://backend:8080
```

### Executar o script de verificação

```bash
# Dentro do WSL ou de um terminal Bash
bash scripts/verificar-cenarios.sh
```

O script roda os 6 cenários nos dois estados e imprime uma tabela de
evidência. Detalhes em [`scripts/verificar-cenarios.sh`](../scripts/verificar-cenarios.sh).

## 8. Trabalhos futuros

Dois pontos reais que sobraram, na ordem de risco:

1. **Remover o `unsafe-inline` do `script-src`.** A CSP atual mantém
   `unsafe-inline` porque o bundle do Vite pode injetar CSS inline que
   quebraria sem ele. É preciso testar o build de produção do Vite com uma
   CSP mais restritiva (com nonce ou hash) e confirmar que as telas continuam
   funcionando. Enquanto isso, o scanner acusa um achado `high` legítimo a
   cada execução.

2. **Decidir o destino do token (`localStorage` vs. cookie `HttpOnly`).** O
   token em `localStorage` é legível por XSS — o próprio cenário de XSS do
   laboratório demonstra isso. Migrar para cookie `HttpOnly` reduziria o
   risco, mas exige alterar o fluxo de autenticação na API (emitir o cookie
   no `Set-Cookie` do login) e no frontend (parar de enviar `Authorization`
   manualmente). A decisão envolve trade-off entre simplicidade do JWT sem
   estado e proteção contra script injection.
