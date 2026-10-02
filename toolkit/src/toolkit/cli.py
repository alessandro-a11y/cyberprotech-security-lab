"""Ponto de entrada CLI do Security Toolkit.

Uso (Fase 1, estrutura):
    python -m toolkit.cli --target http://localhost:5000 --out report.json

Restrito ao ambiente controlado do laboratório. Não utilizar contra
sistemas externos.
"""

from __future__ import annotations

import argparse
from pathlib import Path

from toolkit.report import write_report
from toolkit.scanner import ToolkitScanner


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="CyberProtech Security Toolkit (uso educacional, ambiente controlado)."
    )
    parser.add_argument("--target", required=True, help="Alvo do laboratório, ex.: http://localhost:5000")
    parser.add_argument("--out", default="report.json", help="Arquivo de saída (.json ou .md).")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    result = ToolkitScanner(target=args.target).run()
    write_report(result, Path(args.out))
    print(f"Relatório escrito em {args.out} para o alvo {args.target}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
