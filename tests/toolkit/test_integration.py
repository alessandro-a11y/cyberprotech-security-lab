"""Integração opt-in: requer API do laboratório com a dupla trava habilitada."""
import os

import pytest
import requests

from toolkit.scanner import ToolkitScanner


TARGET = os.getenv("CP_TOOLKIT_INTEGRATION_TARGET")


@pytest.mark.skipif(not TARGET, reason="defina CP_TOOLKIT_INTEGRATION_TARGET para executar contra o laboratório")
def test_scan_muda_quando_headers_de_seguranca_sao_alternados():
    login = requests.post(
        f"{TARGET.rstrip('/')}/api/auth/login",
        json={"username": os.getenv("CP_TOOLKIT_ADMIN", "admin"), "password": os.getenv("CP_TOOLKIT_PASSWORD", "CyberProtech@2026")},
        timeout=10,
    )
    login.raise_for_status()
    headers = {"Authorization": f"Bearer {login.json()['token']}"}

    try:
        disabled = requests.put(f"{TARGET.rstrip('/')}/api/lab/config", headers=headers, json={"securityHeaders": False}, timeout=10)
        disabled.raise_for_status()
        vulnerable = ToolkitScanner(TARGET).run()
        assert any(finding.check == "headers" for finding in vulnerable.findings)

        enabled = requests.put(f"{TARGET.rstrip('/')}/api/lab/config", headers=headers, json={"securityHeaders": True}, timeout=10)
        enabled.raise_for_status()
        corrected = ToolkitScanner(TARGET).run()

        # Com os headers ligados, nenhum deve faltar. O que NÃO pode acontecer é
        # esperar zero achados: a CSP da API mantém 'unsafe-inline' em
        # script-src (o cenário de XSS depende disso), então o scanner continua
        # reclamando — com razão. O que este teste garante é que os headers
        # pararam de faltar.
        faltando = [f.message for f in corrected.findings if f.check == "headers" and "ausente" in f.message]
        assert not faltando, f"headers ainda ausentes com sec-headers ligado: {faltando}"

        # E o inverso: desligado, eles têm de faltar.
        faltando_vuln = [f.message for f in vulnerable.findings if f.check == "headers" and "ausente" in f.message]
        assert faltando_vuln, "com sec-headers desligado nenhum header deveria faltar"
    finally:
        requests.put(f"{TARGET.rstrip('/')}/api/lab/config", headers=headers, json={"securityHeaders": True}, timeout=10)
