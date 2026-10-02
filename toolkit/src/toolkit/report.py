"""Geração de relatórios do toolkit (Fase 4: a implementar).

Formatos planejados: JSON e Markdown, consumidos pela tela
"Resultados do Security Toolkit" do frontend.
"""

from __future__ import annotations

import json
from pathlib import Path

from toolkit.scanner import ScanResult


def to_json(result: ScanResult) -> str:
    """Serializa o resultado agregado em JSON."""
    return json.dumps(
        {
            "target": result.target,
            "findings": [f.__dict__ for f in result.findings],
        },
        indent=2,
        ensure_ascii=False,
    )


def to_markdown(result: ScanResult) -> str:
    """Relatório legível pela tela do portal e por revisão humana."""
    lines = ["# Relatório do Security Toolkit", "", f"Alvo: `{result.target}`", "", "| Severidade | Checagem | Achado |", "|---|---|---|"]
    lines.extend(f"| {item.severity} | {item.check} | {item.message} |" for item in result.findings)
    if not result.findings:
        lines.append("| info | scanner | Nenhum achado nas verificações passivas. |")
    return "\n".join(lines) + "\n"


def write_report(result: ScanResult, output: Path) -> Path:
    """Escreve JSON ou Markdown conforme a extensão pedida."""
    content = to_markdown(result) if output.suffix.lower() in {".md", ".markdown"} else to_json(result) + "\n"
    output.write_text(content, encoding="utf-8")
    return output
