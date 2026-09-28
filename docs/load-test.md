# Teste de carga e comandos para o WSL

Ferramenta de carga em `tests/load`, e o referência de comandos para rodar o
projeto localmente dentro do WSL.

---

# Parte 1 — Comandos para rodar no WSL

## Pré-requisitos

```bash
# dentro do WSL (Ubuntu)
dotnet --list-sdks      # precisa ter o SDK 8.0 (o projeto é net8.0)
docker --version
docker compose version
```

Se o `dotnet` não estiver instalado no WSL:

```bash
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

## Subir o projeto

```bash
# o caminho do projeto dentro do WSL
cd /mnt/d/cyberprotech-security-lab

docker compose up -d --build
```

Acompanhar:

```bash
docker compose ps
docker compose logs -f backend
```

| Serviço | URL (no Windows) | URL (dentro do WSL) |
|---|---|---|
| Frontend | <http://localhost:5173> | <http://localhost:5173> |
| API | <http://localhost:5000> | <http://localhost:5000> |
| Swagger | <http://localhost:5000/swagger> | idem |

Parar (preserva o banco):

```bash
docker compose down
```

Recomeçar do zero, **apagando o banco**:

```bash
docker compose down -v
```

## Conflito de porta do PostgreSQL

O `db` publica a porta no host para o `dotnet test` alcançar. Se já houver um
PostgreSQL local, troque a porta no `.env`:

```bash
cp .env.example .env      # só na primeira vez
nano .env                 # mude DB_PORT=5432 para 5434, por exemplo
docker compose up -d
```

Só o backend, sem o frontend (útil para medir a API isolada):

```bash
docker compose up -d db backend
```

## Rodar a API fora do Docker

Útil para depurar com breakpoint ou medir sem o overhead do nginx.

```bash
# o banco precisa estar no ar
docker compose up -d db

# apontar para a porta publicada
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5434;Database=cyberprotech_lab;Username=postgres;Password=changeme"

cd backend
dotnet run --project API
```

## Testes

```bash
# unitários: não precisam de nada
cd /mnt/d/cyberprotech-security-lab
dotnet test tests/backend/CyberProtech.Tests.Unit

# integração: precisa do banco de teste
docker exec cyberprotech-security-lab-db-1 \
  psql -U postgres -d postgres -c "CREATE DATABASE cyberprotech_test;" 2>/dev/null

export TEST_CONNECTION_STRING="Host=localhost;Port=5434;Database=cyberprotech_test;Username=postgres;Password=changeme"
dotnet test tests/backend/CyberProtech.Tests.Integration

# a solução inteira
dotnet test CyberProtech.slnx
```

## Migrations

```bash
cd backend
dotnet tool restore

dotnet ef migrations add <Nome> --project Infrastructure --startup-project API --output-dir Persistence/Migrations
dotnet ef database update --project Infrastructure --startup-project API
dotnet ef migrations script --project Infrastructure --startup-project API --idempotent
```

## Toolkit

```bash
cd /mnt/d/cyberprotech-security-lab
docker compose --profile toolkit build toolkit
docker compose --profile toolkit run --rm toolkit --target http://backend:8080
```

## Consertar o ambiente quando a VM do Docker travar

Sintoma: `docker ps` mostra containers com `Up 2 seconds`, e qualquer
chamada ao backend devolve `000`.

```bash
docker compose down
docker compose up -d
```

---

# Parte 2 — Teste de carga

## Por que existe

Medição sequencial engana: ela nunca faz duas requisições ao mesmo tempo, e o
banco é justamente o recurso que estraga sob concorrência. O gerador sobe N
workers reais, em paralelo, e reporta vazão e percentis.

## Como usar

```bash
cd /mnt/d/cyberprotech-security-lab

# ajuda com a lista de cenários
dotnet run --project tests/load -c Release -- --ajuda

# exemplo
dotnet run --project tests/load -c Release -- \
  --cenario users --concorrencia 100 --duracao 30
```

Cenários: `users`, `users-busca`, `user-por-id`, `me`, `stats`, `ready`,
`health`, `login`.

Opções principais: `--concorrencia`, `--duracao`, `--rampa`, `--aquecimento`,
`--url`, `--usuario`, `--senha`, `--busca`, `--id`.

## Preparar volume

O cenário `users` com 6 usuários não exercita paginação nem índice. Para medir
em volume:

```bash
docker exec cyberprotech-security-lab-db-1 psql -U postgres -d cyberprotech_lab -q -c "
INSERT INTO users (\"Id\",\"Username\",\"Email\",\"PasswordHash\",\"Role\",\"Bio\",\"CreatedAt\")
SELECT gen_random_uuid(), 'usuario'||lpad(g::text,6,'0'),
       'usuario'||lpad(g::text,6,'0')||'@cyberprotech.lab',
       '\$2a\$12\$'||substr(md5(g::text),1,53), 'User',
       'Bio do usuario '||g, now()
FROM generate_series(1,2000) g ON CONFLICT DO NOTHING;"

# limpar depois
docker exec cyberprotech-security-lab-db-1 psql -U postgres -d cyberprotech_lab -q -c \
  "DELETE FROM users WHERE \"Username\" LIKE 'usuario%';"
```

> Desligue o rate limit antes de medir o login, senão o `429` do laboratório
> mascara a vazão real:
> `curl -X PUT http://localhost:5000/api/lab/config -H "Authorization: Bearer $T" -H 'Content-Type: application/json' -d '{"rateLimit":false}'`

## Resultados medidos

Máquina: WSL2, container único, PostgreSQL 16 em container, **2006 usuários**.
Todos os cenários com 100% de respostas 2xx, exceto o `login` (401 por senha
errada, que é o esperado).

| Cenário | Workers | Vazão | p50 | p95 | p99 |
|---|---|---|---|---|---|
| `health` (sem banco) | 50 | 9551 req/s | 3,6 ms | 12,8 ms | 23,3 ms |
| `users` | 10 | 874 req/s | 8,9 ms | 22,1 ms | 39,0 ms |
| `users` | 50 | 1351 req/s | 31,0 ms | 66,7 ms | 100,7 ms |
| `users` | 100 | ~1070 req/s | 66–93 ms | — | — |
| `users` | 400 | ~720 req/s | 463–608 ms | 710 ms | 796 ms |
| `users-busca` | 50 | 507 req/s | 83,4 ms | 182,9 ms | 252,5 ms |
| `user-por-id` | 100 | 1591 req/s | 50,4 ms | 121,1 ms | 187,8 ms |
| `stats` | 50 | 810 req/s | 54,1 ms | 100,2 ms | 125,0 ms |
| `login` (BCrypt 12) | 1 | 2,1 req/s | 467,9 ms | 544,5 ms | 596,0 ms |
| `login` | 10 | 8,3 req/s | 1098 ms | 1440 ms | 1606 ms |

### Sobre a variância

Rodar o mesmo cenário 3 vezes deu:

- 400 workers: **733 / 795 / 636 req/s** (dispersão ~10%)
- 100 workers: **921 / 967 / 1332 req/s** (dispersão ~40%)

**Leitura única não é confiável nesta máquina.** A VM do WSL disputa CPU com o
Docker e com o próprio host. Por isso a tabela acima traz faixa onde a
repetição variou, e não um número exato. Em hardware dedicado a dispersão cai
muito.

## O que os números mostram

**A API não quebra.** Nenhum 5xx, nenhum timeout, nenhuma falha de transporte em
~120.000 requisições, mesmo com 400 workers simultâneos. A latência cresce
linearmente com a concorrência, que é o comportamento normal de fila.

**O gargalo de leitura é o framework, não o banco.** `health`, que não toca no
PostgreSQL, faz 9551 req/s. `users`, que vai ao banco com índice, faz ~1350.
`user-por-id`, uma linha só, faz 1591. A diferença entre as duas leituras com
banco é pequena: o custo está no caminho HTTP, não no SQL.

**A busca é o caminho de leitura mais lento** (507 req/s contra 1351 da lista,
2,7× mais lento). É esperado: `ILIKE '%termo%'` não pode usar índice, então é
`Seq Scan`. Com 2006 filas leva 2,2 ms; com 200 mil, passaria de 200 ms. Se o
volume crescer muito, aqui é o primeiro lugar a olhar — `pg_trgm` + índice
GIN resolvem.

**O login é limitado por CPU, por desenho.** 2,1 req/s com 1 worker, 8,3 com
10. O ganho não é linear porque o BCrypt custo 12 ocupa o núcleo inteiro. Com o
rate limit ligado, o cenário todo vira `429` a 1,4 ms — o limiter responde
**antes** de gastar BCrypt, que é exatamente o que se quer.

**`/api/admin/stats` faz 3 queries** (total + duas por papel) onde uma
consulta agrupada resolveria. 810 req/s contra 1591 do `user-por-id`. É a única
otimização pendente real.

## Bug encontrado e corrigido: pool de conexões

O teste com 400 workers travou o banco para todo mundo:

```
FATAL: sorry, too many clients already
```

Causa: o pool padrão do Npgsql é **100**, exatamente o `max_connections`
padrão do PostgreSQL. Com os dois iguais, a API pode ocupar todas as conexões
e deixar `psql`, pgAdmin e qualquer outro serviço sem entrada. A API em si não
retornava erro — as requisições ficavam na fila do pool —, então isso só
aparecia de fora.

Corrigido em `DependencyInjection.MontarConnectionString`, com
`Database:MaxPoolSize` (padrão 20). A conta é sempre
`réplicas × pool < max_connections`: com 20 e 3 réplicas sobram 40 das 100.

Depois da correção, medido com 400 workers: **`psql` conecta durante a carga** e
o app segura 1–2 conexões.

Segundo problema no mesmo caminho: `GetValue<int?>` **lança** em valor não
numérico, o que derrubaria a API na subida por causa de um
`Database:MaxPoolSize` escrito errado. Trocado por `int.TryParse`.

Ambos têm teste em `PoolDeConexaoTests`.

## O que não foi medido

- **Memória** e GC: nada de `--dotnet-gcdump` ou similar disponível aqui.
- **Comportamento com múltiplas réplicas** da API: o rate limit é por processo,
  então com 3 réplicas o limite efetivo seria 3×. Precisa de teste com
  `--scale backend=3` e balanceador, que este ambiente não tem.
- **Volume alto de verdade**: o maior teste foi 2006 usuários. A degradação do
  `ILIKE` em 200 mil+ é inferida do plano de execução, não medida.
- **Carga mista** (misturar leitura e login), que é o padrão de uso real.
