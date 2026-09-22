# Security Toolkit — guia base (implementação na Fase 4)

CLI (estrutura pronta na Fase 1):

```bash
# Ambiente local (Python 3.11+)
pip install -r toolkit/requirements.txt
PYTHONPATH=toolkit/src python -m toolkit.cli --target http://localhost:5000 --out report.json

# Via Docker (perfil sob demanda, rede interna do Compose)
docker compose --profile toolkit run --rm toolkit --target http://backend:8080
```

## Módulos

| Módulo                  | Arquivo                          | Status  |
|-------------------------|----------------------------------|---------|
| Orquestrador            | `toolkit/src/toolkit/scanner.py` | esqueleto |
| Headers HTTP            | `toolkit/src/toolkit/headers.py` | planejado (Fase 4) |
| Cookies                 | `toolkit/src/toolkit/cookies.py` | planejado (Fase 4) |
| Endpoints               | `toolkit/src/toolkit/endpoints.py` | planejado (Fase 4) |
| Configuração            | `toolkit/src/toolkit/config_checks.py` | planejado (Fase 4) |
| Relatórios JSON         | `toolkit/src/toolkit/report.py`  | base pronta |

Relatórios em JSON serão exibidos na tela "Resultados do Security Toolkit" do frontend.
