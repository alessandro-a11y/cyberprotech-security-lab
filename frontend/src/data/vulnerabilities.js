// Metadados estáveis dos cenários. O status vem de GET /api/lab/config, pois
// é estado de runtime e não uma etapa fixa do projeto.
export const vulnerabilities = [
  {
    id: 'sqli',
    name: 'SQL Injection',
    owasp: 'A03:2021 · Injection',
    severity: 'critica',
    icon: 'database',
    description: 'Consulta montada por concatenação de string no login ou na busca de usuários.',
  },
  {
    id: 'xss',
    name: 'Cross-Site Scripting',
    owasp: 'A03:2021 · Injection',
    severity: 'alta',
    icon: 'terminal',
    description: 'Campo de perfil renderizado sem escape, executando script no navegador de quem visualiza.',
  },
  {
    id: 'idor',
    name: 'IDOR / Controle de acesso',
    owasp: 'A01:2021 · Broken Access Control',
    severity: 'critica',
    icon: 'key',
    description: 'Endpoint /api/users/{id} devolve dados de outro usuário sem checar o dono da sessão.',
  },
  {
    id: 'auth',
    name: 'Falhas de autenticação',
    owasp: 'A07:2021 · Identification & Auth Failures',
    severity: 'alta',
    icon: 'lock',
    description: 'Sem limite de tentativas, senhas fracas aceitas e sessão sem expiração.',
  },
  {
    id: 'misconfig',
    name: 'Configuração insegura',
    owasp: 'A05:2021 · Security Misconfiguration',
    severity: 'media',
    icon: 'settings',
    description: 'Headers de segurança ausentes, CORS aberto e stack trace exposto em erro.',
  },
  {
    id: 'exposure',
    name: 'Exposição de informações',
    owasp: 'A02:2021 · Cryptographic Failures',
    severity: 'media',
    icon: 'eye',
    description: 'Dados sensíveis devolvidos pela API além do necessário e segredos em configuração.',
  },
];

export const severityBadge = {
  critica: { label: 'CRÍTICA', tone: 'danger' },
  alta: { label: 'ALTA', tone: 'warn' },
  media: { label: 'MÉDIA', tone: 'cyan' },
};

export const statusBadge = {
  planejado: { label: 'planejado', tone: '' },
  vulneravel: { label: 'vulnerável', tone: 'danger' },
  corrigido: { label: 'corrigido', tone: 'green' },
  desconhecido: { label: 'não consultado', tone: '' },
};
