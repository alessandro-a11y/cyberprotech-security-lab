# Guia de demonstração

Roteiro para apresentar o projeto. Cada seção tem o objetivo, o comando e o
que falar.

## Antes de começar

```bash
docker compose up -d --build
```

Aguarde o `backend` ficar `(healthy)`:

```bash
docker compose ps
```

| Serviço | URL |
|---|---|
| Frontend | <http://localhost:5173> |
| API | <http://localhost:5000> |
| Swagger | <http://localhost:5000/swagger> |
| Readiness | <http://localhost:5000/api/health/ready> |

Contas (senha: a que você definiu em `SEED_PASSWORD` no `.env`):

| Usuário | Papel |
|---|---|
| `admin` | Admin |
| `carla.admin` | Admin |
| `aluno01` | User |

> Se a máquina já tiver um PostgreSQL na 5432, ajuste `DB_PORT` no `.env`. Ver
> `.env.example`.

## Roteiro

### 1. O portal funciona (2 min)

1. Abrir <http://localhost:5173> e entrar como `admin`.
2. Navegar em Dashboard, Usuários, Perfil e Administração.
3. Trocar o papel de um usuário em Administração e ver o perfil mudar.

**Falar:** a API real está atrás. Login emite JWT, os papéis controlam o acesso,
e o health check mostra que o PostgreSQL está respondendo.

### 2. A área admin é bloqueada no servidor (1 min)

1. Sair e entrar como `aluno01`.
2. Abrir a tela de Administração.

**Falar:** a tela deixa você abrir a página, mas **a API recusa**:
`AdminController` exige a policy `AdminOnly`, e um `User` leva `403` em tudo de
admin. O aviso na tela descreve o frontend, não o servidor.

Comprovar:

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d "{\"username\":\"aluno01\",\"password\":\"$SEED_PASSWORD\"}" \
  | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')

curl -s -o /dev/null -w "GET /api/admin/stats -> %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/admin/stats
# 403
```

### 3. Injecção de SQL (4 min) — o cenário mais forte

1. Em **Usuários**, no campo de busca, digitar `'` (uma aspa).
2. Ligar o **Modo vulnerável** na tela de Administração.
3. Digitar `' OR '1'='1` e dar Enter.
4. Voltar a busca para uma busca normal e mostrar que funciona de novo.

**Falar:** com o modo ligado, o termo entra concatenado no SQL. A tautologia
vira a condição e a API devolve a tabela inteira. Desligado, o termo viaja como
parâmetro e o resultado é `[]`. Mesmo código, dois comportamentos, alternado
em runtime.

Antes/depois, com o token de admin:

```bash
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" \
  "http://localhost:5000/api/users?search=%27%20OR%20%271%27%3D%271"
# vulnerável: a lista inteira.  corrigido: []
```

Sobre o `UNION SELECT`: ele devolve `500` com `SQLSTATE 42601`. **Isso é a
prova da injeção** — o texto chegou ao parser do Postgres, que reclamou de
sintaxe. Vale mencionar: o payload "bonito" quebraria o SQL ao redor.

### 4. IDOR (2 min)

1. Garantir o modo vulnerável ligado.
2. Como `aluno01`, copiar o id de um Admin da tela de Usuários.
3. Abrir `/api/users/<id-do-admin>` com o token do aluno.
4. Desligar o modo e repetir: agora volta `403`.

**Falar:** o endpoint confia no id que vem na URL. Na versão corrigida ele compara
com o `NameIdentifier` do token — Admin vê qualquer um, usuário comum só o
próprio.

### 5. XSS armazenado (2 min)

1. Com o modo vulnerável ligado, editar o próprio perfil e salvar a bio:
   `<script>alert(document.cookie)</script>`
2. Abrir o perfil — com o frontend integração, o script executa.
3. Desligar o modo, reescrever a bio e ver as tags sumirem.

**Falar:** a falha primária é o frontend renderizar sem escapar. A API ter uma
segunda camada que remove marcação é defesa em profundidade, e a lição é que
**filtro de entrada não é proteção de saída**. Detalhe que vale citar: voltar ao
modo corrigido **não** limpa o que já foi gravado — é preciso reescrever o campo.

### 6. Força bruta (2 min)

1. Logar como `aluno01`.
2. Errar a senha 11 vezes seguidas.

**Falar:** com o limite ligado, a 11ª tentativa volta `429`. Desligado, o
laboratório aceita tentativas ilimitadas — é o cenário de falhas de
autenticação.

### 7. Exposição de informações (1 min)

Este cenário é demonstrado **por linha de comando**, não pela tela. Com **Erros
detalhados** ligado, rode duas requisições de cadastro idênticas em paralelo:

```bash
payload='{"username":"probe_verbose","email":"probe_verbose@lab.invalid","password":"Toolkit1!"}'
for i in 1 2; do curl -sS -X POST http://localhost:5000/api/auth/register -H 'Content-Type: application/json' -d "$payload" & done; wait
```

A primeira cria o usuário; a segunda viola a chave de unicidade no banco e
mostra `exception`, `stackTrace` e `sqlState`. A sonda `verbose-errors` do
Security Toolkit provoca exatamente essa condição.

**Falar:** no estado corrigido o corpo é genérico e o detalhe vai só para o log.
Aqui o vazamento é **integral** quando pedido, nunca parcial.

Combinação para o final da apresentação: ligar **Erros detalhados** junto com o
**Modo vulnerável** e mostrar que a mensagem do Postgres passa a descrever o SQL
vulnerável.

### 8. Configuração insegura (1 min)

```bash
curl -sI http://localhost:5000/api/health | head -8
```

Com **Headers de segurança** ligado aparecem CSP, HSTS, `X-Content-Type-Options`,
`X-Frame-Options` e `Referrer-Policy`. Desligado, nenhum.

## Fechamento: o toolkit

O Security Toolkit roda contra a API nos **dois** estados. É o fecho da
demonstração: ele encontra a vulnerabilidade com o modo ligado e não encontra
com desligado.

```bash
# alvo com o modo vulnerável ligado
docker compose --profile toolkit run --rm toolkit --target http://backend:8080
```

## Voltar ao estado seguro

O botão "Modo vulnerável" começa desligado a cada reinício, porque o estado do
laboratório é em memória. Para zerar na mão:

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d "{\"username\":\"admin\",\"password\":\"$SEED_PASSWORD\"}" \
  | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')

curl -s -X PUT http://localhost:5000/api/lab/config \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"vulnMode":false,"verboseErrors":false,"rateLimit":true,"securityHeaders":true}'
```

## Plano B

Se o Docker não estiver disponível, a API sobe com um PostgreSQL local:

```bash
# apontando a conexão para o banco local
cd backend
dotnet run --project API
```

O resto da demonstração é igual, trocando `http://localhost:5000` pelo que
imprimir no console.

## Avisos para quem apresenta

- **Não exponha em rede confiável com o modo vulnerável ligado.** É um alvo real.
- Só use dados fictícios: e-mail, senha e nome de pessoa.
- O log do container registra um aviso alto quando o modo vulnerável liga. Se
  ele aparecer, é sinal de que alguém esqueceu de desligar:
  `docker compose logs backend | grep -i "MODO VULNER"`
