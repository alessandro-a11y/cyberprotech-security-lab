# Guia de laboratório (uso controlado)

Os cenários de vulnerabilidade do portal. Cada um existe em dois estados,
alternáveis em tempo de execução pela tela de Administração
(`GET/PUT /api/lab/config`).

## Regra de ouro

**A configuração padrão é a versão corrigida.** O modo vulnerável só é
alcançável com duas condições:

1. `Lab:Enabled=true` no ambiente
2. `ASPNETCORE_ENVIRONMENT=Development`

Fora do desenvolvimento, `PUT /api/lab/config` responde `409` mesmo com
`Lab:Enabled=true`. Um deploy com a configuração padrão nunca abre o modo
vulnerável por acidente.

Ligar o modo vulnerável escreve um `LogWarning` alto no log. Vale acompanhar
o log durante a demonstração.

> Não exponha a API em rede confiável com o modo vulnerável ligado. É um alvo
> real para o outro lado da rede.

## Alternando o estado

Na interface, a tela de Administração lê `GET /api/lab/config` e liga/desliga
com `PUT /api/lab/config`:

```bash
SEED_PASSWORD=$(grep '^SEED_PASSWORD=' .env | cut -d= -f2-)
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d "{\"username\":\"admin\",\"password\":\"$SEED_PASSWORD\"}" \
  | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')

# liga o modo vulnerável
curl -X PUT http://localhost:5000/api/lab/config \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"vulnMode":true}'

# volta para a versão corrigida
curl -X PUT http://localhost:5000/api/lab/config \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"vulnMode":false}'
```

Só Admin altera. `User` leva 403.

Os toggles são independentes: dá para mostrar o SQL Injection com os headers de
segurança ainda ligados, por exemplo.

**O estado é em memória.** Reiniciar a API devolve tudo ao padrão
configurado, que é o corrigido. Para recomeçar a demonstração, basta reiniciar.

## Cenários

### 1. SQL Injection — `vuln-mode`

**OWASP A03:2021 · Injection · crítica**

Termo de busca concatenado no SQL em
`EfUserRepository.SearchAsync` (`Infrastructure/Persistence/Repositories/`).
Com o controle desligado, o caminho vulnerável não existe e a busca é LINQ
parametrizado.

```bash
# precisa de token
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/users?search=%27%20OR%20%271%27%3D%271"
```

| Estado | Resultado |
|---|---|
| corrigido | `[]` — o termo viaja como parâmetro |
| vulnerável | a tabela `users` inteira |

O teto de 100 caracteres em `search` também é desativado no modo vulnerável:
ele é um controle real da versão corrigida, mas um `UNION SELECT` precisa de
mais que isso, e sem essa exceção o cenário não seria demonstrável.

Sobre o `UNION SELECT`: ele retorna **500 com `SQLSTATE 42601`**
(erro de sintaxe). Isso é esperado e é a prova de que a injeção aconteceu — o
texto do usuário chegou ao parser do Postgres, que reclamou. O `OR '1'='1'` é o
payload limpo para a demonstração.

Para ler outra tabela de verdade seria preciso acertar as 7 colunas de `users`.
Vale como exercício para o time.

### 2. IDOR / Broken Access Control — `vuln-mode`

**OWASP A01:2021 · crítica**

`GET /api/users/{id}` deixa de comparar o id da rota com o `NameIdentifier` do
token. Qualquer usuário autenticado lê o perfil de qualquer outro, trocando o
id na URL.

```bash
# token do aluno01
curl -H "Authorization: Bearer $TOKEN_ALUNO" \
  http://localhost:5000/api/users/3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e01
```

| Estado | Resultado |
|---|---|
| corrigido | `403` |
| vulnerável | `200` com os dados do admin (`admin@cyberprotech.lab`) |

O aviso de "área restrita" que a tela de Administração exibe hoje descreve o
**frontend**, não a API: `AdminController` exige `AdminOnly` em qualquer
momento. O IDOR é só na leitura de usuário.

### 3. XSS armazenado — `vuln-mode` (componente da API)

**OWASP A03:2021 · alta**

A falha primária **é do frontend**: renderizar a bio com
`dangerouslySetInnerHTML`. A API storing e devolvendo o texto como o usuário
digitou está correta — escapar é responsabilidade de quem renderiza.

O que a API faz é uma segunda camada, opcional: com o controle **desligado**,
`BioSanitizer` remove tags, `on*=` e `javascript:` antes de gravar. Ligado, o
texto passa intacto, que é o estado vulnerável e o que dá payload real para o
frontend executar.

```bash
curl -X PUT http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"email":"aluno01@cyberprotech.lab","bio":"<script>alert(document.cookie)</script>"}'
```

| Estado | Bio gravada |
|---|---|
| corrigido | `alert(document.cookie)` — tags removidas |
| vulnerável | `<script>alert(document.cookie)</script>` |

Dois pontos para a apresentação:

- **Filtro de entrada não é proteção de saída.** O sanitizador reduz o raio,
  mas a correção de verdade é escapar na renderização.
- **Payload já gravado não é retro sanitizado.** Se a bio foi gravada no modo
  vulnerável, voltar para o corrigido não limpa o que já está no banco. É
  preciso reescrever o campo. Isso é proposital e realista: sanitizar na escrita
  não corrige o que já foi contaminado.

### 4. Falhas de autenticação — `rate-limit`

**OWASP A07:2021 · alta**

Com o controle desligado, a partição de rate limit vira "sem limite" e o login
aceita tentativas ilimitadas de senha.

```bash
for i in $(seq 1 20); do
  curl -o /dev/null -w "%{http_code} " -X POST http://localhost:5000/api/auth/login \
    -H 'Content-Type: application/json' -d '{"username":"admin","password":"errada"}'
done
```

| Estado | 20 tentativas de senha errada |
|---|---|
| corrigido | 10 com `401`, 10 com `429` |
| vulnerável | 20 com `401`, nenhum `429` |

Nota de implementação: o estado do controle entra na chave de partição do
rate limiter, porque `RateLimitPartition` memoiza o limitador por chave — sem
isso, virar o toggle não re-avaliava nada.

### 5. Configuração insegura / CORS e headers — `sec-headers`

**OWASP A05:2021 · média**

Com o controle desligado, nenhum header de segurança é enviado.

```bash
curl -I http://localhost:5000/api/health
```

| Estado | Headers |
|---|---|
| corrigido | `Content-Security-Policy`, `Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` |
| vulnerável | nenhum |

É o que o Security Toolkit procura. Para o CORS, o cenário é separado: a policy
é restritiva (`GET/POST/PUT/DELETE`, `Authorization/Content-Type/Accept`, sem
`AllowAnyOrigin`). Afrouxar isso é trabalho da Sprint 6, se o time quiser.

### 6. Exposição de informações — `verbose-errors`

**OWASP A02:2021 · média**

Com o controle ligado, o corpo do erro passa a trazer a exceção, o stack trace
e a mensagem do PostgreSQL — incluindo o SQL que o banco recusou.

```bash
# provoke um 409 disparando o handler de exceção com dois cadastros concorrentes
```

| Estado | Corpo do erro |
|---|---|
| corrigido | `{ title, status, detail, instance, traceId }` — `detail` genérico |
| vulnerável | acrescenta `exception`, `stackTrace`, `sqlState`, `postgresMessage` |

O vazamento é sempre integral sob demanda: ou a resposta traz o diagnóstico
inteiro, ou traz o genérico. Não existe caminho intermediário em que o detalhe
vaze sem ser pedido.

Consequência: com `verbose-errors` **e** `vuln-mode` ligados, a mensagem do
Postgres passa a descrever o SQL vulnerável. Combinação boa para a demonstração,
e ruim para produção — mais um motivo para a dupla trava.

## Matriz de verificação

Estado de cada cenário, o que esperar em cada modo. Os valores são de
verificação real contra o PostgreSQL.

| Cenário | Controle | Corrigido | Vulnerável |
|---|---|---|---|
| SQLi `OR '1'='1'` | `vuln-mode` | `[]` | tabela inteira |
| SQLi `UNION SELECT` | `vuln-mode` | `[]` | `500` / `SQLSTATE 42601` |
| SQLi aspa simples | `vuln-mode` | `200` sem quebra | `200` |
| IDOR ler id alheio | `vuln-mode` | `403` | `200` com o dado |
| IDOR ler o próprio | `vuln-mode` | `200` | `200` |
| XSS `<script>` na bio | `vuln-mode` | tags removidas | payload intacto |
| XSS `onerror=` | `vuln-mode` | removido | intacto |
| Força bruta no login | `rate-limit` | `429` após 10 | nenhum `429` |
| Headers de segurança | `sec-headers` | 5 headers | nenhum |
| Erro com stack trace | `verbose-errors` | genérico | `exception` + `stackTrace` |
| `SQLSTATE` no corpo | `verbose-errors` | ausente | presente |
| Admin em `/api/admin/*` | — | `403` | `403` (não é cenário) |

## O que **não** é cenário

Para o time não procurar bug onde não tem:

- `AdminController` exige `AdminOnly` sempre, com ou sem `vuln-mode`. A área
  administrativa não tem versão vulnerável.
- O hash de senha é BCrypt custo 12 com sal, nos dois estados.
- O token é validado por assinatura nos dois estados: editar o payload (por
  exemplo trocar `User` por `Admin`) devolve `401`.
- Login com usuário inexistente e senha errada devolve a mesma mensagem, nos
  dois estados.
- `PasswordHash` nunca sai na resposta, nos dois estados.

## Regras do laboratório

- Rodar em rede local confiável ou com WSL/Docker isolado. **Não** expor em
  internet.
- Não usar dados reais: nem e-mail, nem senha, nem nome de pessoa.
- O toolkit de segurança (Daniel) deve rodar contra a API nos **dois** estados,
  para mostrar que encontra a vulnerabilidade e que ela some depois da correção.
- Ao terminar a demonstração, voltar todos os controles para o padrão:
  `vuln-mode`, `verbose-errors` desligados; `rate-limit` e `sec-headers`
  ligados. O botão "Modo vulnerável" da interface já começa desligado.
