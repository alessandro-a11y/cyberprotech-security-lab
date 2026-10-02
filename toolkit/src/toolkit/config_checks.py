"""Verificações de configuração, com uma sonda inofensiva de erro controlado."""
from concurrent.futures import ThreadPoolExecutor
from uuid import uuid4

from toolkit.http import request


def analyze_config(target: str) -> list[dict]:
    _, headers, _ = request(target, "/api/health")
    findings = []
    if headers.get("X-CyberProtech-Lab", "").lower() != "on":
        findings.append({"check": "config", "severity": "medium", "message": "A API não confirmou que os headers de segurança estão ativos."})

    # Duas tentativas idênticas de cadastro são inofensivas no laboratório: uma
    # cria uma conta User descartável e a concorrente exercita o handler global.
    # É necessário provocar esse erro para verificar verbose-errors de verdade.
    suffix = uuid4().hex[:12]
    payload = {"username": f"scan_{suffix}", "email": f"scan_{suffix}@lab.invalid", "password": "Toolkit1!"}
    with ThreadPoolExecutor(max_workers=2) as executor:
        responses = list(executor.map(lambda _: request(target, "/api/auth/register", "POST", payload), range(2)))
    body = "\n".join(response[2] for response in responses).lower()
    if ("stack trace" in body) or ((" at " in body) and ("exception" in body)):
        findings.append({"check": "config", "severity": "high", "message": "Resposta pública contém indício de stack trace detalhado."})
    return findings
