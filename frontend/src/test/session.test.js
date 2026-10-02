import { beforeEach, describe, expect, it } from 'vitest';
import {
  endSession,
  getSession,
  getToken,
  initials,
  isExpired,
  startSession,
  updateToken,
} from '../session.js';

const LOGIN = {
  token: 'jwt-de-teste',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { id: 'uuid-1', username: 'admin', email: 'admin@lab.lab', role: 'Admin' },
};

describe('session.js', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('começa sem sessão', () => {
    expect(getSession()).toBeNull();
    expect(getToken()).toBeNull();
  });

  it('guarda token, usuário e expiração', () => {
    const sessao = startSession(LOGIN);

    expect(sessao.token).toBe('jwt-de-teste');
    expect(sessao.username).toBe('admin');
    expect(sessao.role).toBe('Admin');
    expect(sessao.id).toBe('uuid-1');
    expect(sessao.since).toBeTruthy();
    expect(getToken()).toBe('jwt-de-teste');
  });

  it('sobrevive a um "recarregamento" (nova leitura do storage)', () => {
    startSession(LOGIN);

    // Simula o navegador sendo recarregado: o estado do React some, o
    // localStorage não. É exatamente o caso que o sessionStorage não cubria.
    expect(getSession().username).toBe('admin');
  });

  it('updateToken troca só o token, sem perder o usuário', () => {
    startSession(LOGIN);
    updateToken('token-novo', '2030-01-01T00:00:00Z');

    const sessao = getSession();
    expect(sessao.token).toBe('token-novo');
    expect(sessao.username).toBe('admin');
  });

  it('updateToken sem sessão não cria uma', () => {
    expect(updateToken('token')).toBeNull();
    expect(getSession()).toBeNull();
  });

  it('endSession limpa tudo', () => {
    startSession(LOGIN);
    endSession();

    expect(getSession()).toBeNull();
    expect(localStorage.getItem('cp_session')).toBeNull();
  });

  it('detecta token expirado pela data', () => {
    startSession({ ...LOGIN, expiresAt: new Date(Date.now() - 1000).toISOString() });
    expect(isExpired()).toBe(true);
  });

  it('token sem expiresAt não é considerado expirado', () => {
    startSession({ token: 't', user: LOGIN.user });
    expect(isExpired()).toBe(false);
  });

  it('lixo no storage não derruba a aplicação', () => {
    localStorage.setItem('cp_session', '{isto nao e json');

    expect(getSession()).toBeNull();
    expect(getToken()).toBeNull();
  });

  it('sessão sem token não vale', () => {
    localStorage.setItem('cp_session', JSON.stringify({ user: LOGIN.user }));

    expect(getSession()).toBeNull();
  });

  describe('initials', () => {
    it.each([
            // Nome único pega a segunda letra: comportamento original do componente.
      ['admin', 'AD'],
      ['maria.souza', 'MS'],
      ['joao.lima', 'JL'],
      ['pedro.alves', 'PA'],
      ['', '?'],
    ])('"%s" vira "%s"', (entrada, esperado) => {
      expect(initials(entrada)).toBe(esperado);
    });
  });
});