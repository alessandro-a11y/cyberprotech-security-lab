import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { startSession } from '../session.js';

const getUsers = vi.fn(() => Promise.resolve([]));
const register = vi.fn(() => Promise.resolve({ id: '2', username: 'novo' }));

vi.mock('../api/client.js', () => ({
  API_BASE_URL: 'http://localhost:5000',
  USE_SAMPLE_DATA: false,
  api: {
    getUsers: (...a) => getUsers(...a),
    register: (...a) => register(...a),
  },
}));

import Users from '../pages/Users.jsx';

const renderUsers = () => render(<MemoryRouter><Users /></MemoryRouter>);

// A busca é debounced em 300ms, então toda espera precisa passar por waitFor.
const ultimaBusca = () => getUsers.mock.calls[getUsers.mock.calls.length - 1];

describe('Users', () => {
  beforeEach(() => {
    localStorage.clear();
    startSession({ token: 't', expiresAt: new Date(Date.now() + 60_000).toISOString(), user: { id: '1', username: 'admin', role: 'Admin' } });
    getUsers.mockReset();
    register.mockReset();
    getUsers.mockResolvedValue([{ id: 'abcdef12-3456', username: 'admin', email: 'admin@lab.test', role: 'Admin' }]);
  });

  it('faz a busca pela API e manda o filtro de papel na query, não no cliente', async () => {
    renderUsers();
    await waitFor(() => expect(getUsers).toHaveBeenCalled());
    expect(ultimaBusca()[0]).toBe('');
    expect(ultimaBusca()[1]).toEqual({ role: undefined });

    await userEvent.click(screen.getByRole('tab', { name: 'Admins' }));
    await waitFor(() => expect(ultimaBusca()[1]).toEqual({ role: 'Admin' }));

    await userEvent.click(screen.getByRole('tab', { name: 'Usuários' }));
    await waitFor(() => expect(ultimaBusca()[1]).toEqual({ role: 'User' }));

    await userEvent.click(screen.getByRole('tab', { name: 'Todos' }));
    await waitFor(() => expect(ultimaBusca()[1]).toEqual({ role: undefined }));
  });

  it('envia o termo digitado no parâmetro search (com debounce)', async () => {
    renderUsers();
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(1));

    await userEvent.type(screen.getByPlaceholderText(/buscar por usuário/i), 'ma');

    // Debounce: digitar 2 letras não gera 2 requisições.
    expect(getUsers).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2), { timeout: 2000 });
    expect(ultimaBusca()[0]).toBe('ma');
  });

  it('recusa a busca e mostra a mensagem da API, com botão de nova tentativa', async () => {
    getUsers.mockRejectedValue(new Error('Falha na conexão com o banco.'));
    renderUsers();
    expect(await screen.findByText('Falha na conexão com o banco.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /tentar de novo/i })).toBeInTheDocument();
  });

  it('abre o formulário de cadastro e valida a senha com 8, o mesmo mínimo da API', async () => {
    renderUsers();
    await screen.findByText('admin');
    expect(screen.queryByLabelText(/senha/i)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /novo usuário/i }));
    // minLength=8 e não 12: senha de 8 é aceita pela API e não pode ser barrada aqui.
    expect(screen.getByLabelText(/senha/i)).toHaveAttribute('minlength', '8');
  });

  it('mostra a mensagem da API quando o cadastro é recusado', async () => {
    register.mockRejectedValue(new Error('E-mail já cadastrado.'));
    renderUsers();
    await screen.findByText('admin');

    await userEvent.click(screen.getByRole('button', { name: /novo usuário/i }));
    await userEvent.type(screen.getByLabelText(/^usuário$/i), 'novo');
    await userEvent.type(screen.getByLabelText(/e-mail/i), 'novo@lab.test');
    await userEvent.type(screen.getByLabelText(/^senha$/i), 'CyberProtech@2026');
    await userEvent.click(screen.getByRole('button', { name: 'Cadastrar' }));

    expect(await screen.findByText('E-mail já cadastrado.')).toBeInTheDocument();
  });
});