"""CyberProtech Security Toolkit — pacote base (Fase 1: arquitetura).

Análises reais (headers, cookies, endpoints, configurações e relatórios)
serão implementadas na Fase 4, sempre restritas ao ambiente controlado do laboratório.
"""

from toolkit.scanner import ScanResult, ToolkitScanner

__all__ = ["ScanResult", "ToolkitScanner"]
__version__ = "0.1.0"
