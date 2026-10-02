import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { startSession } from '../session.js';

const getUsers = vi.fn(() => Promise.resolve([{ id: '1', username: 'admin', role: 'Admin' }]));
const getStats = vi.fn(() => Promise.resolve({ totalUsers: 1, admins: 1, adminPercentage: 100 }));
const getLabConfig = vi.fn(() => Promise.resolve({ writable: false, toggles: [{ id: 'vuln-mode', label: 'Modo vulnerável', hint: 'religa erros', on: false }] }));
const updateLabConfig = vi.fn(() => Promise.resolve({ writable: false, toggles: [] }));

vi.mock('../api/client.js', () => ({
  API_BASE_URL: 'http://localhost:5000',
  USE_SAMPLE_DATA: false,
  api: {
    getUsers: (...a) => getUsers(...a),
    getStats: (...a) => getStats(...a),
    getLabConfig: (...a) => getLabConfig(...a),
    updateLabConfig: (...a) => updateLabConfig(...a),
    changeRole: vi.fn(() => Promise.resolve()),
    deleteUser: vi.fn(() => Promise.resolve()),
  },
}));

import Admin from '../pages/Admin.jsx';

const renderAdmin = () =>
  render(<MemoryRouter><Admin /></MemoryRouter>);

describe('Admin', () => {
  beforeEach(() => {
    localStorage.clear();
    startSession({ token: 't', expiresAt: new Date(Date.now() + 60_000).toISOString(), user: { id: '1', username: 'admin', role: 'Admin' } });
    getUsers.mockClear(); getStats.mockClear(); getLabConfig.mockClear(); updateLabConfig.mockReset();
    getUsers.mockResolvedValue([{ id: '1', username: 'admin', role: 'Admin' }]);
    getStats.mockResolvedValue({ totalUsers: 1, admins: 1, adminPercentage: 100 });
  });

  it('espelha os toggles que vieram do servidor, não os do módulo de exemplo', async () => {
    getLabConfig.mockResolvedValue({
      writable: true,
      toggles: [{ id: 'vuln-mode', label: 'Modo vulnerável', hint: 'religa erros', on: true }],
    });
    renderAdmin();
    await waitFor(() => expect(screen.getByRole('switch', { name: 'Modo vulnerável' })).toHaveAttribute('aria-checked', 'true'));
  });

  it('deixa os switches desabilitados quando a dupla trava impede escrita (writable=false)', async () => {
    getLabConfig.mockResolvedValue({
      writable: false,
      toggles: [{ id: 'vuln-mode', label: 'Modo vulnerável', hint: 'religa erros', on: false }],
    });
    renderAdmin();
    const sw = await screen.findByRole('switch', { name: 'Modo vulnerável' });
    expect(sw).toBeDisabled();
    // Desabilitado de verdade: o clique não pode chegar na API.
    await userEvent.click(sw);
    expect(updateLabConfig).not.toHaveBeenCalled();
  });

  it('reverte o estado otimista quando a API recusa com 409', async () => {
    getLabConfig.mockResolvedValue({
      writable: true,
      toggles: [{ id: 'vuln-mode', label: 'Modo vulnerável', hint: 'religa erros', on: false }],
    });
    updateLabConfig.mockRejectedValue(Object.assign(new Error('Fora de desenvolvimento ou laboratório desabilitado.'), { status: 409 }));
    renderAdmin();
    const sw = await screen.findByRole('switch', { name: 'Modo vulnerável' });
    await userEvent.click(sw);
    // A UI liga na hora, mas volta ao estado do servidor quando o PUT falha.
    await waitFor(() => expect(sw).toHaveAttribute('aria-checked', 'false'));
    expect(await screen.findByText(/laborat.rio desabilitado/i)).toBeInTheDocument();
  });

  it('não deixa rebaixar ou remover a própria conta logada', async () => {
    renderAdmin();
    await screen.findByText('admin');
    expect(screen.getByRole('button', { name: 'Rebaixar' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Remover' })).toBeDisabled();
  });

  it('avisa que a API bloqueia o acesso quando a sessão não é Admin', async () => {
    startSession({ token: 't', expiresAt: new Date(Date.now() + 60_000).toISOString(), user: { id: '9', username: 'aluno', role: 'User' } });
    renderAdmin();
    expect(await screen.findByText(/Broken Access Control/i)).toBeInTheDocument();
  });
});