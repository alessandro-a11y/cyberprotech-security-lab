import { sampleApi } from '../data/sampleUsers.js';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000').replace(/\/$/, '');

// Enquanto a API não tem busca, bio e login, as telas de usuário usam
// dados de exemplo. Com VITE_USE_SAMPLE_DATA=false tudo vem da API.
const USE_SAMPLE_DATA = (import.meta.env.VITE_USE_SAMPLE_DATA ?? 'true') !== 'false';

async function request(path) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) {
    throw new Error(`API respondeu ${response.status} em ${path}`);
  }
  return response.json();
}

export const api = {
  getHealth: () => request('/api/health'),
  // A busca vai para a API (?search=) de propósito: é o ponto de entrada
  // do cenário de SQL Injection (Fase 3). Não filtrar só no navegador.
  getUsers: (search = '') =>
    USE_SAMPLE_DATA
      ? sampleApi.getUsers(search)
      : request(`/api/users${search ? `?search=${encodeURIComponent(search)}` : ''}`),
  getUserById: (id) => (USE_SAMPLE_DATA ? sampleApi.getUserById(id) : request(`/api/users/${id}`)),
};

export { API_BASE_URL, USE_SAMPLE_DATA };
