"""Orquestrador base das análises do Security Toolkit (Fase 1: esqueleto).

Cada módulo de análise (headers, cookies, endpoints, config) exporá uma
função que recebe o alvo do laboratório e devolve achados estruturados.
As implementações reais entram na Fase 4.
"""

from __future__ import annotations

from dataclasses import dataclass, field


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
        """Fase 1: retorna estrutura vazia. Análises reais na Fase 4."""
        return ScanResult(target=self.target, findings=[])
