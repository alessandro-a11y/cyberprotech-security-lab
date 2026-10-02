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
    assert len(endpoints.analyze_endpoints("http://lab")) == 3


def test_endpoints_usa_get_e_nao_head(monkeypatch):
    """HEAD em /swagger devolve 404 na API; com HEAD o achado nunca aparecia."""
    from toolkit import endpoints

    def fake(target, path, method="GET", body=None):
        # Reproduz a API real: HEAD em /swagger* é 404, GET responde 200.
        if method == "HEAD":
            return 404, {}, ""
        return 200, {}, ""

    monkeypatch.setattr(endpoints, "request", fake)
    mensagens = [f["message"] for f in endpoints.analyze_endpoints("http://lab")]
    assert any("Swagger" in m for m in mensagens), mensagens
    assert any("Readiness" in m for m in mensagens), mensagens


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
