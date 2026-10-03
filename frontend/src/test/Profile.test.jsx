import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { startSession } from '../session.js';

const getUserById = vi.fn();
const getLabConfig = vi.fn();

vi.mock('../api/client.js', () => ({
  USE_SAMPLE_DATA: false,
  api: {
    getUserById: (...args) => getUserById(...args),
    getLabConfig: (...args) => getLabConfig(...args),
    updateProfile: vi.fn(),
    changePassword: vi.fn(),
  },
}));

import Profile from '../pages/Profile.jsx';

const payload = '<img src=x onerror=alert(1)>';
const user = {
  id: 'user-1', username: 'aluno', email: 'aluno@lab.test', role: 'User', bio: payload,
  createdAt: '2026-01-01T00:00:00Z',
};

function renderProfile(id = 'user-1') {
  return render(
    <MemoryRouter initialEntries={[`/profile/${id}`]}>
      <Routes><Route path="/profile/:id" element={<Profile />} /></Routes>
    </MemoryRouter>,
  );
}

describe('Profile', () => {
  beforeEach(() => {
    localStorage.clear();
    startSession({ token: 'token', expiresAt: new Date(Date.now() + 60_000).toISOString(), user });
    getUserById.mockReset();
    getLabConfig.mockReset();
    getUserById.mockResolvedValue(user);
  });

  it('renderiza a bio como HTML no perfil quando vuln-mode está ligado', async () => {
    getLabConfig.mockResolvedValue({ toggles: [{ id: 'vuln-mode', on: true }] });
    const { container } = renderProfile('outro-usuario');

    await waitFor(() => expect(container.querySelector('img')).toBeInTheDocument());
    expect(container.querySelector('img')).toHaveAttribute('src', 'x');
  });

  it('mantém a bio como texto quando vuln-mode está desligado', async () => {
    getLabConfig.mockResolvedValue({ toggles: [{ id: 'vuln-mode', on: false }] });
    const { container } = renderProfile();

    expect(await screen.findByText(payload)).toBeInTheDocument();
    expect(container.querySelector('img')).not.toBeInTheDocument();
  });

  it.each([null, ''])('exibe o placeholder para bio vazia com vuln-mode ligado (%p)', async (bio) => {
    getUserById.mockResolvedValue({ ...user, bio });
    getLabConfig.mockResolvedValue({ toggles: [{ id: 'vuln-mode', on: true }] });
    renderProfile();

    expect(await screen.findByText('Sem bio.')).toBeInTheDocument();
  });
});
