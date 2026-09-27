# Contrato de integração — API ↔ Frontend

Documento para o Alfredo (frontend) e para o time em geral. Descreve
exatamente o que a API espera e devolve, e o que o frontend precisa mudar.

Base: `backend/README.md` tem a lista completa de endpoints. Aqui está o
fluxo e as decisões que não são óbvias na tabela.

## 1. Configurar o frontend

```
# frontend/.env
VITE_API_BASE_URL=http://localhost:5000
VITE_USE_SAMPLE_DATA=false
```

`VITE_USE_SAMPLE_DATA=false` é o que desliga `sampleApi` e faz o
`frontend/src/api/client.js` chamar a API real. Sem isso, nada muda.

> Atenção: o build do Docker do frontend copia `.env` em build. Depois de
> mudar, é preciso `docker compose up -d --build frontend`.

## 2. Guardar o token

A API devolve o JWT no corpo do login:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-09-26T21:15:33Z",
  "user": { "id": "...", "username": "admin", "email": "...", "role": "Admin", "bio": "...", "createdAt": "..." }
}
```

Enviar em toda requisição autenticada:

```js
headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }
```

**Onde guardar.** O `frontend/src/session.js` hoje usa `sessionStorage` com um
objeto de protótipo. A recomendação do time:

- guardar o token em **`localStorage`**, não em `sessionStorage`. O motivo é
  funcional, não de segurança: `sessionStorage` morre ao fechar a aba, e o
  `GET /api/auth/me` existe justamente para restaurar a sessão depois de um
  recarregamento. Com `sessionStorage`, recarregar a página perde o token.
- XSS consegue ler `localStorage`. Como o laboratório tem um cenário de XSS
  (`vulnerabilities.js`), isso é **material de estudo**: o cenrio vai
  conseguir roubar o token de uma vítima, e é exatamente o que um cookie
  `HttpOnly` evitaria. Vale usar isso na apresentação em vez de esconder.

## 3. Login

```js
const res = await fetch(`${API}/api/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username, password }),
})
if (res.status === 401) throw new Error('Usuário ou senha inválidos.')
const { token, user } = await res.json()
```

A mensagem de erro da API é **exatamente** a que o `Login.jsx` já mostra
(`Login.jsx:30`), então o texto não precisa mudar.

Para redirecionar, `user.role` vem pronto: `"Admin"` ou `"User"`.

Contas de teste (senha `CyberProtech@2026`):

| Usuário | Papel |
|---|---|
| `admin` | Admin |
| `carla.admin` | Admin |
| `aluno01` | User |
| `maria.souza` | User |
| `joao.lima` | User |
| `pedro.alves` | User |

> O login tem rate limit de 10 tentativas por minuto por IP. Erros de digitação
> seguidos vão dar **429** — vale tratar no `Login.jsx`, senão parece bug.

## 4. Restaurar a sessão

```js
const res = await fetch(`${API}/api/auth/me`, { headers })
// 200 -> sessão válida; 401 -> token expirado ou inválido, limpar e redirecionar
```

Esse é o substituto do `getSession()` do `session.js`. O `Layout.jsx` deve
chamar no mount e, em 401, mandar para `/login`.

`expiresAt` permite checar localmente antes de gastar uma requisição.

## 5. Perfil

| Ação | Chamada |
|---|---|
| Ver perfil | `GET /api/auth/me` |
| Ver outro usuário | `GET /api/users/{id}` |
| Editar o próprio | `PUT /api/auth/me` com `{ email, bio }` |
| Trocar senha | `POST /api/auth/change-password` com `{ currentPassword, newPassword }` |

Os dois formulários do `Profile.jsx` (linhas 106 e 158) já correspondem a isso.
Basta ligar o `onSubmit` e tirar o `disabled`.

Detalhes que evitam surpresa:

- **`username` não é editável.** A API ignora; o campo deve seguir read-only,
  como já está.
- **`PUT /api/auth/me` devolve um token novo.** Se o e-mail mudou, o antigo
  ainda carrega o e-mail anterior. Salve o token novo.
- **Trocar senha não invalida o token antigo** (limitação conhecida, ver
  `backend/README.md`). O `Logout` do frontend é local: apagar o token.
- `bio` volta `null` quando vazio — renderizar como `"Sem bio."`, que o
  `Profile.jsx` já faz.

## 6. Área administrativa

| Ação | Chamada | Quem pode |
|---|---|---|
| Contagens | `GET /api/admin/stats` | Admin |
| Trocar papel | `PUT /api/admin/users/{id}/role` com `{ role }` | Admin |
| Editar bio | `PUT /api/users/{id}` com `{ bio }` | Admin |
| Remover | `DELETE /api/users/{id}` | Admin |

`GET /api/admin/stats` devolve o que o `Admin.jsx` hoje calcula no cliente:

```json
{ "totalUsers": 6, "admins": 2, "regularUsers": 4, "adminPercentage": 33 }
```

Para trocar papel, os valores aceitos são exatamente `"Admin"` e `"User"`.
Qualquer outro (`"admin"` minúsculo, `"SuperAdmin"`) devolve **400**.

Três casos devolvem **409** e precisam de mensagem própria na tela, porque
significam "o pedido faz sentido, mas não agora":

- admin tentando alterar o próprio papel;
- rebaixar o último Admin;
- admin tentando remover a própria conta.

## 7. Bloqueio por papel no cliente

Hoje o `Admin.jsx:42` mostra um aviso de que um `User` consegue abrir a página,
e diz que isso é o cenário de Broken Access Control. A API **não** tem esse
problema: `AdminController` exige `AdminOnly` e um `User` leva **403** em tudo.

Ou seja: o aviso do `Admin.jsx` descreve o frontend, não a API. Quando os
endpoints entrarem, o `fetch` passa a receber 403, e aí sim dá para redirecionar
para quem não é Admin. Manter o aviso é decisão de demonstração — dá para
mostrar os dois lados.

## 8. Estados de erro que o frontend precisa tratar

| Status | Quando | O que fazer |
|---|---|---|
| 400 | Validação (`ValidationProblemDetails`, com `errors` por campo) | Mostrar junto ao campo |
| 401 | Sem token, token inválido ou expirado, senha errada | Limpar sessão, ir para `/login` |
| 403 | Não é dono do recurso, ou não é Admin | Mensagem de permissão |
| 404 | Recurso não existe | Estado "não encontrado" (o `Profile.jsx` já tem) |
| 409 | E-mail em uso, trava de papel | Mostrar a mensagem do `title` |
| 429 | Rate limit de login | Avisar para aguardar |
| 500 | Erro interno | Genérico; o `traceId` vai no log do servidor |

Erros vêm como ProblemDetails: `{ title, status, detail, instance, traceId }`.
O `title` é escrito para ser exibido.

## 9. CORS

O backend liberou explicitamente:

- **Origens:** `http://localhost:5173` e `http://127.0.0.1:5173` (mais o que
  estiver em `Frontend:BaseUrl`)
- **Métodos:** `GET`, `POST`, `PUT`, `DELETE`
- **Headers:** `Authorization`, `Content-Type`, `Accept`
- **Sem** `AllowAnyOrigin` e **sem** credenciais

Se o preflight falhar, quase sempre é porque a origem não está na lista. Em
desenvolvimento, `ASPNETCORE_ENVIRONMENT=Development` já adiciona as duas URLs
acima automaticamente. Fora dele, é preciso declarar em `Frontend:BaseUrl`
(vários valores separados por `;`).

O `AllowAnyHeader`/`AllowAnyMethod` que existiam antes foram removidos de
propósito: "CORS aberto" é o cenário `misconfig` do laboratório.

## 10. Controles do laboratório

`GET /api/lab/config` devolve a posição real dos quatro toggles. O `Admin.jsx`
pode trocar o array `initialToggles` fixo por essa resposta — assim a tela deixa
de mostrar `vuln-mode: on` com a API inteira corrigida.

```js
const { toggles, writable } = await (await fetch(`${API}/api/lab/config`, { headers })).json()
```

`writable: false` significa que os switches devem aparecer desabilitados (fora
de desenvolvimento, ou com `Lab:Enabled=false`).

Estado atual: os quatro toggles existem como contrato, mas **nada ancora atrás
deles ainda** — a API está inteira no estado corrigido. A Sprint 5 ancore os
cenários. Então marcar `vuln-mode` **não** abre nada por enquanto; quando
âncorar, os toggles passam a ter efeito de verdade.

## Ordem sugerida de integração

1. `client.js`: ler `VITE_API_BASE_URL` já funciona; adicionar `Authorization`
2. `session.js`: guardar `token` + `user` + `expiresAt`
3. `Login.jsx`: `POST /api/auth/login` no `handleSubmit` (hoje é `sampleApi`)
4. `Layout.jsx`: `GET /api/auth/me` no mount, 401 → `/login`
5. `Profile.jsx`: ligar os dois formulários
6. `Admin.jsx`: `GET /api/admin/stats` no lugar das contagens locais, e os
   toggles lendo `/api/lab/config`
7. `Users.jsx` / `Dashboard.jsx`: devem passar a funcionar sozinhos, já que
   `getUsers` é o mesmo caminho
