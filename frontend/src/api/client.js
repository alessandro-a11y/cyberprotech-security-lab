const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000').replace(/\/$/, '');

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
  getUsers: () => request('/api/users'),
  getUserById: (id) => request(`/api/users/${id}`),
};

export { API_BASE_URL };
