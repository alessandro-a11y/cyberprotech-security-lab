import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import Login from '../pages/Login.jsx';
import { getSession, startSession } from '../session.js';

vi.mock('../api/client.js', () => ({
  api: { login: vi.fn() },
  USE_SAMPLE_DATA: false,
  API_BASE_URL: 'http://localhost:5000',
}));

const { api } = await import('../api/client.js');

function erroApi(status, mensagem) {
  const erro = new Error(mensagem);
  erro.status = status;
  return erro;
}

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/" element={<h1>Dashboard</h1>} />
      </Routes>
    </MemoryRouter>,
  );
}

const LOGIN_OK = {
  token: 'jwt-de-teste',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { id: 'uuid-1', username: 'admin', role: 'Admin' },
};

describe('Login.jsx', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  afterEach(() => vi.restoreAllMocks());

  it('exige usuário e senha antes de chamar a API', async () => {
    const usuario = userEvent.setup();
    renderLogin();

    await usuario.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByText(/informe usuário e senha/i)).toBeInTheDocument();
    expect(api.login).not.toHaveBeenCalled();
  });

  it('chama a API com o usuário sem espaços nas pontas', async () => {
    const usuario = userEvent.setup();
    api.login.mockResolvedValue(LOGIN_OK);
    renderLogin();

    await usuario.type(screen.getByPlaceholderText(/aluno01/i), '  admin  ');
    await usuario.type(screen.getByPlaceholderText(/••••/), 'CyberProtech@2026');
    await usuario.click(screen.getByRole('button', { name: /entrar/i }));

    await waitFor(() => expect(api.login).toHaveBeenCalledWith('admin', 'CyberProtech@2026'));
  });

  it('guarda o token e o usuário na sessão ao entrar', async () => {
    const usuario = userEvent.setup();
    api.login.mockResolvedValue(LOGIN_OK);
    renderLogin();

    await usuario.type(screen.getByPlaceholderText(/aluno01/i), 'admin');
    await usuario.type(screen.getByPlaceholderText(/••••/), 'CyberProtech@2026');
    await usuario.click(screen.getByRole('button', { name: /entrar/i }));

    await waitFor(() => expect(getSession()).not.toBeNull());
    expect(getSession().token).toBe('jwt-de-teste');
    expect(getSession().role).toBe('Admin');
  });

  it('navega para o dashboard depois do login', async () => {
    const usuario = userEvent.setup();
    api.login.mockResolvedValue(LOGIN_OK);
    renderLogin();

    await usuario.type(screen.getByPlaceholderText(/aluno01/i), 'admin');
    await usuario.type(screen.getByPlaceholderText(/••••/), 'CyberProtech@2026');
    await usuario.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByRole('heading', { name: /dashboard/i })).toBeInTheDocument();
  });

  it('mostra a mensagem da API em credenciais inválidas (401)', async () => {
    const usuario = userEvent.setup();
    api.login.mockRejectedValue(erroApi(401, 'Usuário ou senha inválidos.'));
    renderLogin();

    await usuario.type(screen.getByPlaceholderText(/aluno01/i), 'admin');
    await usuario.type(screen.getByPlaceholderText(/••••/), 'errada');
    await usuario.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByText(/usuário ou senha inválidos/i)).toBeInTheDocument();
    expect(getSession()).toBeNull();
  });

  it('trata 429 com mensagem de aguardar, e não como senha inválida', async () => {
    // Sem este caso, o rate limit do laboratório pareceria bug de senha.
    const usuario = userEvent.setup();
    api.login.mockRejectedValue(erroApi(429, 'Muitas tentativas.'));
    renderLogin();

    await usuario.type(screen.getByPlaceholderText(/aluno01/i), 'admin');
    await usuario.type(screen.getByPlaceholderText(/••••/), 'errada');
    await usuario.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByText(/muitas tentativas/i)).toBeInTheDocument();
    expect(getSession()).toBeNull();
  });

  it('mostra erro genérico quando a API está fora do ar', async () => {
    const usuario = userEvent.setup();
    api.login.mockRejectedValue(erroApi(0, 'Não foi possível falar com a API. Ela está no ar?'));
    renderLogin();

    await usuario.type(screen.getByPlaceholderText(/aluno01/i), 'admin');
    await usuario.type(screen.getByPlaceholderText(/••••/), 'senha');
    await usuario.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByText(/não foi possível falar com a api/i)).toBeInTheDocument();
  });

  it('desabilita o botão enquanto a requisição está em andamento', async () => {
    const usuario = userEvent.setup();

    // Promessa que só resolve quando a gente mandar: dá para observar o meio.
    let liberar;
    api.login.mockReturnValue(
      new Promise((resolve) => {
        liberar = () => resolve(LOGIN_OK);
      }),
    );

    renderLogin();

    await usuario.type(screen.getByPlaceholderText(/aluno01/i), 'admin');
    await usuario.type(screen.getByPlaceholderText(/••••/), 'senha');
    await usuario.click(screen.getByRole('button', { name: /entrar/i }));

    const botao = await screen.findByRole('button', { name: /entrando/i });
    expect(botao).toBeDisabled();

    // Um segundo clique não deve gerar outra requisição.
    await usuario.click(botao);
    expect(api.login).toHaveBeenCalledTimes(1);

    liberar();
    await waitFor(() => expect(getSession()).not.toBeNull());
  });

  it('já redireciona para o dashboard se a sessão existir', async () => {
    startSession(LOGIN_OK);
    renderLogin();

    expect(await screen.findByRole('heading', { name: /dashboard/i })).toBeInTheDocument();
  });
});