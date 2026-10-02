"""Mapeamento passivo de superfícies públicas conhecidas do laboratório."""
from toolkit.http import request


def analyze_endpoints(target: str) -> list[dict]:
    findings = []
    for path, severity, description in (
        ("/swagger", "low", "Swagger está exposto sem autenticação."),
        ("/api/health/ready", "info", "Readiness endpoint responde sem autenticação."),
    ):
        status, _, _ = request(target, path, "HEAD")
        if 200 <= status < 400:
            findings.append({"check": "endpoints", "severity": severity, "message": description})
    return findings
