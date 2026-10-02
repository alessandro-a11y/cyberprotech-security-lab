"""Mapeamento passivo de superfícies públicas conhecidas do laboratório."""
from toolkit.http import request


def analyze_endpoints(target: str) -> list[dict]:
    findings = []
    # GET, e não HEAD: o roteamento do ASP.NET responde 404 a HEAD em /swagger
    # (que é um redirect para /swagger/index.html), então com HEAD este check
    # nunca disparava mesmo com o Swagger exposto. urllib segue o redirect e
    # devolve o status final. São leituras passivas de superfície pública.
    for path, severity, description in (
        ("/swagger/index.html", "low", "Swagger está exposto sem autenticação."),
        ("/swagger/v1/swagger.json", "low", "Documento OpenAPI está exposto sem autenticação."),
        ("/api/health/ready", "info", "Readiness endpoint responde sem autenticação."),
    ):
        status, _, _ = request(target, path, "GET")
        if 200 <= status < 400:
            findings.append({"check": "endpoints", "severity": severity, "message": description})
    return findings
