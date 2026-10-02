import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { startSession } from '../session.js';

vi.mock('../api/client.js', () => ({
  API_BASE_URL: 'http://localhost:5000',
  USE_SAMPLE_DATA: true,
  api: { getHealth: vi.fn(() => Promise.resolve({ status: 'healthy' })), getUsers: vi.fn(() => Promise.resolve([{ id: '1', username: 'aluno', role: 'User' }])), getStats: vi.fn(() => Promise.resolve(null)) },
}));

import Dashboard from '../pages/Dashboard.jsx';

describe('Dashboard', () => {
  beforeEach(() => {
    localStorage.clear();
    startSession({ token: 'token', expiresAt: new Date(Date.now() + 60_000).toISOString(), user: { username: 'admin', role: 'Admin' } });
  });

  it('usa a lista como fallback quando stats devolve null em dados de exemplo', async () => {
    render(<MemoryRouter><Dashboard /></MemoryRouter>);
    expect(await screen.findByText('1')).toBeInTheDocument();
    expect(screen.getByText('Olá, admin')).toBeInTheDocument();
  });
});
