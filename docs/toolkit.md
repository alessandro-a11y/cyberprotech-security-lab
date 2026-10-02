# Security Toolkit

O toolkit faz checagens HTTP passivas contra o laboratório. A única sonda ativa
é um par de cadastros idênticos, inofensivos e descartáveis, necessário para
verificar se `verbose-errors` expõe stack trace.
Ele verifica headers, cookies emitidos sem credenciais, superfícies públicas e
configuração observável. Não envia payloads de exploração.

```bash
# Ambiente local (Python 3.11+). Use um venv: em Ubuntu o Python do sistema é
# "externally managed" e o pip recusa instalar (PEP 668).
python3 -m venv toolkit/.venv
toolkit/.venv/bin/pip install -r toolkit/requirements.txt

PYTHONPATH=toolkit/src toolkit/.venv/bin/python -m toolkit.cli \
  --target http://localhost:5000 --out report.json

# Via Docker (perfil sob demanda, rede interna do Compose)
docker compose --profile toolkit run --rm toolkit --target http://backend:8080
```

## Testes

```bash
toolkit/.venv/bin/pip install -r toolkit/requirements.txt   # traz o pytest

# 6 testes de unidade: rodam sozinhos
PYTHONPATH=toolkit/src toolkit/.venv/bin/python -m pytest tests/toolkit

# + o teste de integração, que exige a API no ar
CP_TOOLKIT_INTEGRATION_TARGET=http://localhost:5000 \
  PYTHONPATH=toolkit/src toolkit/.venv/bin/python -m pytest tests/toolkit
```

O teste de integração é **opt-in** por `CP_TOOLKIT_INTEGRATION_TARGET`: sem a
variável ele é pulado, e por isso **não roda na CI**. Ele alterna
`sec-headers` e confere que os headers param de faltar. Com a API fora ele
**falha** por `ConnectionError`, e não passa vazio — foi assim que uma asserção
errada dele passou meses sem ninguém ver.

Variáveis: `CP_TOOLKIT_ADMIN` (padrão `admin`) e `CP_TOOLKIT_PASSWORD` (padrão:
o valor de `SEED_PASSWORD` no seu `.env`).

## Módulos

| Módulo                  | Arquivo                          | Função  |
|-------------------------|----------------------------------|---------|
| Orquestrador            | `toolkit/src/toolkit/scanner.py` | agrega os achados |
| Headers HTTP            | `toolkit/src/toolkit/headers.py` | CSP, HSTS, XCTO, XFO e Referrer-Policy |
| Sessão                  | `toolkit/src/toolkit/cookies.py` | risco do Bearer em localStorage versus cookie HttpOnly |
| Endpoints               | `toolkit/src/toolkit/endpoints.py` | Swagger e readiness públicos |
| Configuração            | `toolkit/src/toolkit/config_checks.py` | confirmação do middleware e erros expostos |
| Relatórios              | `toolkit/src/toolkit/report.py`  | JSON e Markdown |

Relatórios podem ser JSON ou Markdown (`--out report.md`).
## Execução

O toolkit faz no máximo duas requisições POST de cadastro idênticas por scan,
além das leituras passivas; não envia payloads de exploração.
Gere JSON ou Markdown com:

```bash
PYTHONPATH=toolkit/src python -m toolkit.cli --target http://localhost:5000 --out report.md
```

O status `corrigido` no portal significa cenário implementado, mas protegido pelo
controle de laboratório correspondente; não significa que o cenário deixou de existir.
