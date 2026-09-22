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


def write_report(result: ScanResult, output: Path) -> Path:
    """Escreve o relatório em disco (JSON por padrão na Fase 1)."""
    output.write_text(to_json(result) + "\n", encoding="utf-8")
    return output
