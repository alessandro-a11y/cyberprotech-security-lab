import { describe, expect, it } from 'vitest';
import { statusDoCenario } from '../labStatus.js';

describe('statusDoCenario', () => {
  it('relaciona SQLi, IDOR e XSS ao modo vulnerável', () => {
    const toggles = [{ id: 'vuln-mode', on: true }];
    expect(statusDoCenario('sqli', toggles).status).toBe('vulneravel');
    expect(statusDoCenario('idor', toggles).status).toBe('vulneravel');
    expect(statusDoCenario('xss', toggles).status).toBe('vulneravel');
  });

  it('distingue a proteção ativa da ausência de implementação', () => {
    const status = statusDoCenario('auth', [{ id: 'rate-limit', on: false }]);
    expect(status.status).toBe('corrigido');
    expect(status.detail).toContain('implementado');
  });
});
