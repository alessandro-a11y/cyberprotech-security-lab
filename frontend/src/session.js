// Sessão de PROTÓTIPO (MVP): só guarda quem "entrou" para a interface
// poder mostrar nome, papel e o próprio perfil. Não é autenticação. O login
// real (token/cookie emitido pela API) entra na Fase 2 e substitui este arquivo.
const KEY = 'cp_session';

export function getSession() {
  try {
    const raw = sessionStorage.getItem(KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

// `user` vem dos dados de exemplo ({ id, username, role }).
export function startSession(user) {
  const session = {
    id: user.id,
    username: user.username,
    role: user.role,
    since: new Date().toISOString(),
  };
  try {
    sessionStorage.setItem(KEY, JSON.stringify(session));
  } catch {
    // Sem storage disponível: a sessão vale só até recarregar a página.
  }
  return session;
}

export function endSession() {
  try {
    sessionStorage.removeItem(KEY);
  } catch {
    // Nada a limpar.
  }
}

export function initials(name = '') {
  const parts = name.replace(/[._-]+/g, ' ').trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? parts[0]?.[1] ?? '')).toUpperCase() || '?';
}
