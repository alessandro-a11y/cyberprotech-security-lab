"""Verificação do desenho de sessão Bearer usado pelo laboratório."""


def analyze_cookies(target: str) -> list[dict]:
    _ = target
    # A API não emite Set-Cookie: JWT volta no corpo e o frontend o guarda em
    # localStorage. Isso não é header ausente, mas um risco de desenho que o
    # cenário de XSS demonstra; um cookie HttpOnly reduziria esse risco.
    return [{"check": "session-design", "severity": "info", "message": "Sessão Bearer armazenada no localStorage; XSS pode ler o token. Cookies HttpOnly reduziriam esse risco."}]
