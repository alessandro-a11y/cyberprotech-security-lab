// Cenários do laboratório (Fase 3). `status` evolui conforme o time avança:
// 'planejado' → 'vulneravel' → 'corrigido'.
export const vulnerabilities = [
  {
    id: 'sqli',
    name: 'SQL Injection',
    owasp: 'A03:2021 · Injection',
    severity: 'critica',
    icon: 'database',
    description: 'Consulta montada por concatenação de string no login ou na busca de usuários.',
    status: 'planejado',
  },
  {
    id: 'xss',
    name: 'Cross-Site Scripting',
    owasp: 'A03:2021 · Injection',
    severity: 'alta',
    icon: 'terminal',
    description: 'Campo de perfil renderizado sem escape, executando script no navegador de quem visualiza.',
    status: 'planejado',
  },
  {
    id: 'idor',
    name: 'IDOR / Controle de acesso',
    owasp: 'A01:2021 · Broken Access Control',
    severity: 'critica',
    icon: 'key',
    description: 'Endpoint /api/users/{id} devolve dados de outro usuário sem checar o dono da sessão.',
    status: 'planejado',
  },
  {
    id: 'auth',
    name: 'Falhas de autenticação',
    owasp: 'A07:2021 · Identification & Auth Failures',
    severity: 'alta',
    icon: 'lock',
    description: 'Sem limite de tentativas, senhas fracas aceitas e sessão sem expiração.',
    status: 'planejado',
  },
  {
    id: 'misconfig',
    name: 'Configuração insegura',
    owasp: 'A05:2021 · Security Misconfiguration',
    severity: 'media',
    icon: 'settings',
    description: 'Headers de segurança ausentes, CORS aberto e stack trace exposto em erro.',
    status: 'planejado',
  },
  {
    id: 'exposure',
    name: 'Exposição de informações',
    owasp: 'A02:2021 · Cryptographic Failures',
    severity: 'media',
    icon: 'eye',
    description: 'Dados sensíveis devolvidos pela API além do necessário e segredos em configuração.',
    status: 'planejado',
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
};
