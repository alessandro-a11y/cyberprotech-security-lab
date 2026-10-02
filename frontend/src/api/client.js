import { sampleApi } from '../data/sampleUsers.js';
import { endSession, getToken } from '../session.js';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000').replace(/\/$/, '');

// Enquanto a API não tem busca, bio e login, as telas de usuário usam
// dados de exemplo. Com VITE_USE_SAMPLE_DATA=false tudo vem da API.
const USE_SAMPLE_DATA = (import.meta.env.VITE_USE_SAMPLE_DATA ?? 'true') !== 'false';

/**
 * Erro de API, com o status preservado.
 *
 * A mensagem exibida vem do campo `title` do ProblemDetails, que a API escreve
 * para ser mostrada. Manter o status permite tratar 401 (sessão expirada) e 429
 * (rate limit de login) separadamente.
 */
export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

function mensagemDe(response, fallback) {
  // A API responde JSON; qualquer outra coisa cai no texto genérico.
  try {
    const clone = response.clone();
    return clone
      .json()
      .then((corpo) => corpo?.title || corpo?.detail || fallback)
      .catch(() => fallback);
  } catch {
    return Promise.resolve(fallback);
  }
}

async function request(path, { method = 'GET', body, auth = true } = {}) {
  const headers = { Accept: 'application/json' };

  if (body !== undefined) headers['Content-Type'] = 'application/json';

  if (auth) {
    const token = getToken();
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  let response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
  } catch {
    // Falha de rede: o navegador lança TypeError('Failed to fetch'). Mostrar
    // essa string crua na tela não ajuda ninguém — e ela também aparece quando
    // a API está desligada, ou quando o CORS barra antes de chegar no servidor.
    throw new ApiError('Não foi possível falar com a API. Ela está no ar?', 0);
  }

  if (!response.ok) {
    const fallback = `A API respondeu ${response.status} em ${path}`;
    const error = new ApiError(await mensagemDe(response, fallback), response.status);
    // Login e cadastro são públicos: um 401 ali é uma mensagem de credencial,
    // não uma sessão expirada. Nas demais chamadas, não manter um JWT inválido
    // evita deixar a pessoa presa numa tela que só responde 401.
    if (response.status === 401 && auth) {
      endSession();
      window.dispatchEvent(new Event('cp:unauthorized'));
    }
    throw error;
  }

  // DELETE devolve 204 sem corpo; GET em lista de 0 itens devolve [].
  if (response.status === 204) return null;

  const texto = await response.text();
  return texto ? JSON.parse(texto) : null;
}

/** Formata query ignorando valores vazios. */
function query(params) {
  const pares = Object.entries(params)
    .filter(([, valor]) => valor !== undefined && valor !== null && valor !== '')
    .map(([chave, valor]) => `${encodeURIComponent(chave)}=${encodeURIComponent(valor)}`);
  return pares.length ? `?${pares.join('&')}` : '';
}

export const api = {
  // --- autenticação ---
  login: (username, password) =>
    request('/api/auth/login', { method: 'POST', body: { username, password }, auth: false }),

  register: (dados) =>
    request('/api/auth/register', { method: 'POST', body: dados, auth: false }),

  me: () => request('/api/auth/me'),

  updateProfile: (dados) => request('/api/auth/me', { method: 'PUT', body: dados }),

  changePassword: (currentPassword, newPassword) =>
    request('/api/auth/change-password', { method: 'POST', body: { currentPassword, newPassword } }),

  // --- usuários ---
  getUsers: (search = '', { role } = {}) =>
    USE_SAMPLE_DATA
      ? sampleApi.getUsers(search)
      : request(`/api/users${query({ search, role, pageSize: 100 })}`),

  getUserById: (id) => (USE_SAMPLE_DATA ? sampleApi.getUserById(id) : request(`/api/users/${id}`)),

  deleteUser: (id) => request(`/api/users/${id}`, { method: 'DELETE' }),

  // --- área administrativa ---
  getStats: () => (USE_SAMPLE_DATA ? null : request('/api/admin/stats')),

  changeRole: (id, role) => request(`/api/admin/users/${id}/role`, { method: 'PUT', body: { role } }),

  // --- laboratório ---
  getLabConfig: () => (USE_SAMPLE_DATA ? null : request('/api/lab/config')),

  updateLabConfig: (controles) => request('/api/lab/config', { method: 'PUT', body: controles }),

  // --- saúde ---
  // Sempre real, mesmo com dados de exemplo: é o indicador da conexão.
  getHealth: () => request('/api/health', { auth: false }),

  getReady: () => request('/api/health/ready', { auth: false }),
};

export { API_BASE_URL, USE_SAMPLE_DATA };
