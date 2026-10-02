"""Checagens passivas dos headers de segurança."""
from toolkit.http import request

REQUIRED = {
    "Content-Security-Policy": "high",
    "Strict-Transport-Security": "medium",
    "X-Content-Type-Options": "medium",
    "X-Frame-Options": "medium",
    "Referrer-Policy": "low",
}


def analyze_headers(target: str) -> list[dict]:
    status, headers, _ = request(target)
    if not status:
        return [{"check": "headers", "severity": "high", "message": "Não foi possível conectar ao alvo."}]
    findings = []
    for name, severity in REQUIRED.items():
        value = headers.get(name)
        if not value:
            findings.append({"check": "headers", "severity": severity, "message": f"{name} ausente ou permissivo demais."})
        elif name == "Content-Security-Policy":
            policy = value.lower()
            script = policy.split("script-src", 1)[1].split(";", 1)[0] if "script-src" in policy else ""
            style = policy.split("style-src", 1)[1].split(";", 1)[0] if "style-src" in policy else ""
            if "'unsafe-inline'" in script:
                findings.append({"check": "headers", "severity": "high", "message": "Content-Security-Policy permite unsafe-inline em script-src."})
            if "'unsafe-inline'" in style:
                findings.append({"check": "headers", "severity": "low", "message": "Content-Security-Policy permite unsafe-inline em style-src."})
    return findings
