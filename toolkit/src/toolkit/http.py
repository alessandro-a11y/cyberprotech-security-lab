"""Cliente HTTP mínimo e deliberadamente passivo do toolkit."""
from __future__ import annotations

from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen
import json


def request(target: str, path: str = "/", method: str = "GET", body: dict | None = None):
    """Faz somente GET/HEAD e devolve status, headers e corpo sem lançar por 4xx."""
    url = f"{target.rstrip('/')}{path}"
    try:
        data = json.dumps(body).encode() if body is not None else None
        headers = {"User-Agent": "CyberProtech-Toolkit/0.1"}
        if data:
            headers["Content-Type"] = "application/json"
        with urlopen(Request(url, method=method, headers=headers, data=data), timeout=8) as response:
            return response.status, response.headers, response.read().decode("utf-8", errors="replace")
    except HTTPError as error:
        return error.code, error.headers, error.read().decode("utf-8", errors="replace")
    except URLError as error:
        return 0, {}, str(error.reason)
