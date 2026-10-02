from pathlib import Path

from toolkit.report import to_markdown, write_report
from toolkit.scanner import Finding, ScanResult, ToolkitScanner


def test_headers_reporta_header_ausente(monkeypatch):
    from toolkit import headers
    monkeypatch.setattr(headers, "request", lambda *_: (200, {}, ""))
    assert any(item["check"] == "headers" for item in headers.analyze_headers("http://lab"))


def test_cookies_reporta_risco_do_desenho_bearer():
    from toolkit import cookies
    finding = cookies.analyze_cookies("http://lab")[0]
    assert finding["check"] == "session-design"
    assert finding["severity"] == "info"


def test_endpoints_mapeia_uma_rota_publica(monkeypatch):
    from toolkit import endpoints
    monkeypatch.setattr(endpoints, "request", lambda *_: (200, {}, ""))
    assert len(endpoints.analyze_endpoints("http://lab")) == 2


def test_config_reconhece_modo_lab(monkeypatch):
    from toolkit import config_checks
    monkeypatch.setattr(config_checks, "request", lambda *_: (200, {"X-CyberProtech-Lab": "on"}, "ok"))
    assert config_checks.analyze_config("http://lab") == []


def test_scanner_agrega_os_modulos(monkeypatch):
    import toolkit.scanner as scanner
    monkeypatch.setattr(scanner, "analyze_headers", lambda _: [{"check": "headers", "severity": "low", "message": "x"}])
    monkeypatch.setattr(scanner, "analyze_cookies", lambda _: [])
    monkeypatch.setattr(scanner, "analyze_endpoints", lambda _: [])
    monkeypatch.setattr(scanner, "analyze_config", lambda _: [])
    assert ToolkitScanner("http://lab").run().findings[0].check == "headers"


def test_relatorio_markdown(tmp_path: Path):
    result = ScanResult("http://lab", [Finding("headers", "high", "CSP ausente")])
    output = tmp_path / "report.md"
    write_report(result, output)
    assert "CSP ausente" in to_markdown(result)
    assert output.read_text(encoding="utf-8").startswith("# Relatório")
