"""Orquestrador base das análises do Security Toolkit (Fase 1: esqueleto).

Cada módulo de análise (headers, cookies, endpoints, config) exporá uma
função que recebe o alvo do laboratório e devolve achados estruturados.
As implementações reais entram na Fase 4.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from toolkit.config_checks import analyze_config
from toolkit.cookies import analyze_cookies
from toolkit.endpoints import analyze_endpoints
from toolkit.headers import analyze_headers


@dataclass
class Finding:
    """Um achado individual de uma análise."""

    check: str
    severity: str  # "info" | "low" | "medium" | "high"
    message: str


@dataclass
class ScanResult:
    """Resultado agregado de uma execução do toolkit."""

    target: str
    findings: list[Finding] = field(default_factory=list)


class ToolkitScanner:
    """Executa as análises disponíveis contra o alvo do laboratório."""

    def __init__(self, target: str) -> None:
        self.target = target

    def run(self) -> ScanResult:
        findings = []
        for check in (analyze_headers, analyze_cookies, analyze_endpoints, analyze_config):
            findings.extend(Finding(**item) for item in check(self.target))
        return ScanResult(target=self.target, findings=findings)
