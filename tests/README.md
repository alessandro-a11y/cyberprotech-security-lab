# Testes do backend

Duas suítes xUnit, ambas em net8.0 e ambas em `CyberProtech.slnx`.

| Projeto | Tipo | Precisa de banco | Casos |
|---|---|---|---|
| `backend/CyberProtech.Tests.Unit` | Unidade | não | 62 |
| `backend/CyberProtech.Tests.Integration` | Integração (API real) | **sim** | 37 |

## Por que a suíte de integração precisa do PostgreSQL

O laboratório tem um cenário de SQL Injection, e um banco em memória não serviria
para nada: ele não tem parser de SQL, nem índice único, nem
`SQLSTATE 23505`. A suíte sobe a API de verdade com `WebApplicationFactory` contra
um PostgreSQL de verdade, para que os cenários sejam testados no mesmo banco da
aplicação.

Sem banco, os testes **não falham**: eles são marcados como pulados, com aviso no
output. Falhar por falta de infraestrutura esconderia regressão de verdade.

## Rodar

```bash
# unitários (não precisam de nada)
dotnet test tests/backend/CyberProtech.Tests.Unit

# integração: precisa de um PostgreSQL
export TEST_CONNECTION_STRING="Host=localhost;Port=5432;Database=cyberprotech_test;Username=postgres;Password=changeme"
dotnet test tests/backend/CyberProtech.Tests.Integration

# a solução inteira
dotnet test CyberProtech.slnx
```

### Preparar o banco de teste

O banco precisa existir; as migrations e o seed rodam sozinhos na primeira
inicialização da API dentro do teste.

```bash
# com o compose no ar
docker exec -it cyberprotech-security-lab-db-1 \
  psql -U postgres -d postgres -c "CREATE DATABASE cyberprotech_test;"

# recomeçar do zero
docker exec -it cyberprotech-security-lab-db-1 \
  psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS cyberprotech_test;"
docker exec -it cyberprotech-security-lab-db-1 \
  psql -U postgres -d postgres -c "CREATE DATABASE cyberprotech_test;"
```

A porta depende do `DB_PORT` do seu `.env`. O `docker-compose.yml` publica a
porta do banco justamente para isto — sem isso, `dotnet test` na máquina não
alcança o banco do container.

Use um banco **descartável**. A suíte escreve nele (cria usuários, troca senha,
altera bio).

## Cobertura

### Unidade (62 casos)

| Arquivo | O que trava |
|---|---|
| `BcryptPasswordHasherTests` | custo 12, sal por senha, hash inválido não lança, `VerifyOrDummy` gasta tempo parecido |
| `JwtTokenServiceTests` | `sub` como string, papel na claim, issuer/audience/validade, mínimo real de 32 bytes |
| `JwtOptionsValidationTests` | chave ausente ou curta é recusada na subida, e a chave padrão do `appsettings.json` passa |
| `LabStateTests` | **a dupla trava**: fora de Development nunca grava, mesmo com `Enabled=true` |
| `CorsAndSanitizerTests` | origens do CORS (incluindo `127.0.0.1`), allowlists restritas, sanitizador da bio nos dois estados |

### Integração (37 casos)

| Classe | O que trava |
|---|---|
| `AutenticacaoTests` | login, 401, mensagem genérica sem enumeração de usuário, 400 de validação, troca de senha, hash que não vaza, escalada de papel bloqueada no cadastro |
| `CenariosLaboratorioTests` | **cada cenário nos dois estados**: SQLi, IDOR, XSS, mais o que *não* é cenário |
| `RateLimitTests` | 10 tentativas e depois 429; desligado, nenhum 429 |
| `ConfiguracaoDaFactoryTests` | a própria factory injeta a configuração (falha alto, e com o JSON à vista) |

## Três decisões que evitam teste instável

**1. O rate limit vem desligado por padrão na factory.** Com ele ligado, a janela
de 10/min por IP é compartilhada pela classe inteira e os testes começam a se
rejeitar com `429`. O comportamento do limite tem classe própria, com factory
própria, porque `RateLimitPartition` memoiza o limitador por chave dentro da
instância da aplicação — uma classe que queima a janela envenenaria as outras.

**2. O paralelismo está desligado** (`AssemblyInfo.cs`). As classes compartilham
um único PostgreSQL, e o cenário de XSS reescreve a bio do `aluno01`. Em
paralelo, uma classe leria o que a outra está escrevendo.

**3. O token é cacheado na factory.** Sem cache, um `IAsyncLifetime` que
autentica antes de cada teste somaria dezenas de logins por classe. Pior: um
`Skip.If(token is null)` transformaria falta de banco em teste pulado em
silêncio, escondendo regressão.

## Detalhe de ambiente apanhado aqui

O `WebApplicationFactory` precisa que a configuração do teste **vença** o
`appsettings.json`. Usar `ConfigureAppConfiguration` não bastava: o `Program.cs`
lia a conexão do `appsettings` e o teste batia no PostgreSQL errado da máquina
(a porta 5432 padrão). A factory usa `builder.UseSetting`, que escreve na
configuração do host e tem precedência.

`ConfiguracaoDaFactoryTests` existe para pegar isso na hora se um dia voltar a
acabar errado.

## Toolkit

Os testes unitários passivos do toolkit ficam em `tests/toolkit/` e são
executados na CI junto com a compilação:

```bash
PYTHONPATH=toolkit/src pytest tests/toolkit
```

Para o teste de integração (opt-in), suba o laboratório com `Lab:Enabled=true`
em `Development` e defina `CP_TOOLKIT_INTEGRATION_TARGET`. Ele alterna somente
`securityHeaders` e sempre restaura o estado seguro no `finally`.
