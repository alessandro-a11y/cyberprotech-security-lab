import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { startSession } from '../session.js';

// A flag precisa estar FORA antes de o módulo ser avaliado: o client.js lê
// import.meta.env.VITE_USE_SAMPLE_DATA no topo, e sem isso o padrão seria
// "true" (dados de exemplo) e getUsers nunca chamaria a API — os testes
// passariam sem exercitar o cliente HTTP.
// O import é dinâmico por causa dessa ordem.
vi.stubEnv('VITE_USE_SAMPLE_DATA', 'false');

const { ApiError, api } = await import('../api/client.js');

const BASE = 'http://localhost:5000';

function responder(status, corpo) {
  return {
    ok: status >= 200 && status < 300,
    status,
    clone: () => ({ json: async () => corpo }),
    json: async () => corpo,
    text: async () => (corpo === undefined ? '' : JSON.stringify(corpo)),
  };
}

const SESSAO = {
  token: 'jwt-de-teste',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { id: 'uuid-1', username: 'admin', role: 'Admin' },
};

describe('client.js', () => {
  beforeEach(() => {
    localStorage.clear();
    fetch = vi.fn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('login', () => {
    it('envia POST sem header de autorização', async () => {
      fetch.mockResolvedValue(responder(200, { token: 't', user: {} }));

      await api.login('admin', 'senha');

      const [url, opcoes] = fetch.mock.calls[0];
      expect(url).toBe(`${BASE}/api/auth/login`);
      expect(opcoes.method).toBe('POST');
      // Sem sessão, não pode mandar Authorization.
      expect(opcoes.headers.Authorization).toBeUndefined();
      expect(JSON.parse(opcoes.body)).toEqual({ username: 'admin', password: 'senha' });
    });

    it('traduz 401 em ApiError com a mensagem da API', async () => {
      fetch.mockResolvedValue(responder(401, { title: 'Usuário ou senha inválidos.' }));

      await expect(api.login('admin', 'errada')).rejects.toThrowError(
        new ApiError('Usuário ou senha inválidos.', 401),
      );
    });

    it('não encerra sessão ao receber 401 do login público', async () => {
      startSession(SESSAO);
      fetch.mockResolvedValue(responder(401, { title: 'Usuário ou senha inválidos.' }));
      await api.login('admin', 'errada').catch(() => {});
      expect(localStorage.getItem('cp_session')).not.toBeNull();
    });

    it('preserva o status 429 (o Login.jsx depende dele)', async () => {
      fetch.mockResolvedValue(responder(429, { title: 'Muitas tentativas.' }));

      const erro = await api.login('admin', 'errada').catch((e) => e);

      expect(erro.status).toBe(429);
    });

    it('cai em mensagem genérica quando a API não devolve JSON', async () => {
      fetch.mockResolvedValue({
        ok: false,
        status: 502,
        clone: () => ({ json: async () => Promise.reject(new Error('sem json')) }),
        json: async () => ({}),
        text: async () => 'gateway estourado',
      });

      const erro = await api.me().catch((e) => e);

      expect(erro.status).toBe(502);
      expect(erro.message).toContain('502');
    });
  });

  describe('com sessão', () => {
    beforeEach(() => startSession(SESSAO));

    it('manda Authorization: Bearer em toda chamada autenticada', async () => {
      fetch.mockResolvedValue(responder(200, { id: 'uuid-1' }));

      await api.me();

      expect(fetch.mock.calls[0][1].headers.Authorization).toBe('Bearer jwt-de-teste');
    });

    it('me envia GET autenticado', async () => {
      fetch.mockResolvedValue(responder(200, { id: 'uuid-1' }));

      const usuario = await api.me();

      expect(fetch.mock.calls[0][0]).toBe(`${BASE}/api/auth/me`);
      expect(usuario.id).toBe('uuid-1');
    });

    it('limpa a sessão quando uma rota protegida devolve 401', async () => {
      fetch.mockResolvedValue(responder(401, { title: 'Expirado' }));
      await api.me().catch(() => {});
      expect(localStorage.getItem('cp_session')).toBeNull();
    });

    it('updateProfile usa PUT com o corpo', async () => {
      fetch.mockResolvedValue(responder(200, { user: {}, token: 'novo' }));

      await api.updateProfile({ email: 'novo@lab.lab', bio: 'oi' });

      const [url, opcoes] = fetch.mock.calls[0];
      expect(url).toBe(`${BASE}/api/auth/me`);
      expect(opcoes.method).toBe('PUT');
      expect(JSON.parse(opcoes.body)).toEqual({ email: 'novo@lab.lab', bio: 'oi' });
    });

    it('changePassword usa POST com os dois campos', async () => {
      fetch.mockResolvedValue(responder(204));

      await api.changePassword('atual', 'nova');

      const [url, opcoes] = fetch.mock.calls[0];
      expect(url).toBe(`${BASE}/api/auth/change-password`);
      expect(JSON.parse(opcoes.body)).toEqual({ currentPassword: 'atual', newPassword: 'nova' });
    });

    it('204 devolve null, e não tenta fazer parse do vazio', async () => {
      fetch.mockResolvedValue(responder(204));

      await expect(api.deleteUser('uuid-9')).resolves.toBeNull();
    });

    it('busca monta a query ignorando valores vazios', async () => {
      fetch.mockResolvedValue(responder(200, []));

      await api.getUsers('');

      // search vazio não vai na URL; pageSize sempre vai.
      expect(fetch.mock.calls[0][0]).toBe(`${BASE}/api/users?pageSize=100`);
    });

    it('busca com termo e papel monta os dois', async () => {
      fetch.mockResolvedValue(responder(200, []));

      await api.getUsers('admin', { role: 'Admin' });

      expect(fetch.mock.calls[0][0]).toBe(`${BASE}/api/users?search=admin&role=Admin&pageSize=100`);
    });

    it('manda o termo de busca como parâmetro, não na URL solta', async () => {
      fetch.mockResolvedValue(responder(200, []));

      await api.getUsers("' OR '1'='1");

      const url = fetch.mock.calls[0][0];

      // O React monta a URL e não tenta "limpar" o payload: decide o que fazer
      // com ele é o servidor, e é o que torna o cenário de SQL Injection
      // demonstrável. Note que encodeURIComponent deixa o apóstrofo literal
      // (comportamento padrão do JS), então a proteção real é a parametrização
      // do lado da API, não isto aqui.
      expect(url).toContain('search=');
      expect(url).toContain('%20OR%20'); // espaços codificados
      expect(url.split('search=')[1].split('&')[0]).toBe("'%20OR%20'1'%3D'1");
    });

    it('changeRole envia PUT no endpoint do admin', async () => {
      fetch.mockResolvedValue(responder(200, {}));

      await api.changeRole('uuid-2', 'Admin');

      const [url, opcoes] = fetch.mock.calls[0];
      expect(url).toBe(`${BASE}/api/admin/users/uuid-2/role`);
      expect(opcoes.method).toBe('PUT');
    });

    it('getLabConfig e updateLabConfig batem nas rotas do laboratório', async () => {
      fetch.mockResolvedValue(responder(200, { toggles: [] }));

      await api.getLabConfig();
      expect(fetch.mock.calls[0][0]).toBe(`${BASE}/api/lab/config`);

      await api.updateLabConfig({ vulnMode: true });
      const [url, opcoes] = fetch.mock.calls[1];
      expect(url).toBe(`${BASE}/api/lab/config`);
      expect(opcoes.method).toBe('PUT');
      expect(JSON.parse(opcoes.body)).toEqual({ vulnMode: true });
    });

    it('getHealth não manda autorização (é público)', async () => {
      fetch.mockResolvedValue(responder(200, { status: 'healthy' }));

      await api.getHealth();

      expect(fetch.mock.calls[0][1].headers.Authorization).toBeUndefined();
    });
  });

  it('sem sessão, nenhuma requisição autenticada leva Authorization', async () => {
    fetch.mockResolvedValue(responder(401, { title: 'Unauthorized' }));

    await api.me().catch(() => {});

    expect(fetch.mock.calls[0][1].headers.Authorization).toBeUndefined();
  });
});
