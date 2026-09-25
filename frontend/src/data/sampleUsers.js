// DADOS DE EXEMPLO — fictícios, só para o protótipo.
// Usados enquanto a API não tem busca, perfil com bio e login.
// Desligar com VITE_USE_SAMPLE_DATA=false quando os endpoints existirem.
export const sampleUsers = [
  {
    id: '3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e01',
    username: 'admin',
    email: 'admin@cyberprotech.lab',
    role: 'Admin',
    bio: 'Conta administrativa do laboratório.',
    createdAt: '2026-09-01T09:00:00Z',
  },
  {
    id: '3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e02',
    username: 'aluno01',
    email: 'aluno01@cyberprotech.lab',
    role: 'User',
    bio: 'Estudante de segurança da informação.',
    createdAt: '2026-09-02T14:20:00Z',
  },
  {
    id: '3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e03',
    username: 'maria.souza',
    email: 'maria.souza@cyberprotech.lab',
    role: 'User',
    bio: 'Analista de suporte. Gosta de café e de logs bem escritos.',
    createdAt: '2026-09-03T10:05:00Z',
  },
  {
    id: '3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e04',
    username: 'joao.lima',
    email: 'joao.lima@cyberprotech.lab',
    role: 'User',
    bio: 'Desenvolvedor front-end em treinamento.',
    createdAt: '2026-09-05T16:40:00Z',
  },
  {
    id: '3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e05',
    username: 'carla.admin',
    email: 'carla@cyberprotech.lab',
    role: 'Admin',
    bio: 'Responsável pela gestão de acessos.',
    createdAt: '2026-09-08T08:30:00Z',
  },
  {
    id: '3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e06',
    username: 'pedro.alves',
    email: 'pedro.alves@cyberprotech.lab',
    role: 'User',
    bio: '',
    createdAt: '2026-09-12T11:15:00Z',
  },
];

const delay = (value) => new Promise((resolve) => setTimeout(() => resolve(value), 250));

export const sampleApi = {
  getUsers(search = '') {
    const q = search.trim().toLowerCase();
    return delay(
      sampleUsers.filter(
        (u) => !q || u.username.toLowerCase().includes(q) || u.email.toLowerCase().includes(q),
      ),
    );
  },
  getUserById(id) {
    const user = sampleUsers.find((u) => u.id === id);
    return user ? delay(user) : Promise.reject(new Error(`API respondeu 404 em /api/users/${id}`));
  },
  findByUsername(username) {
    return sampleUsers.find((u) => u.username.toLowerCase() === username.toLowerCase()) ?? null;
  },
};
