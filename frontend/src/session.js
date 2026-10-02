// Sessão do portal: guarda o token JWT emitido pela API e o usuário que veio
// junto no login.
//
// O token fica em localStorage (e não em sessionStorage) porque o
// GET /api/auth/me existe justamente para restaurar a sessão depois de um
// recarregamento de página — com sessionStorage o token morre ao fechar a aba.
//
// Aviso de segurança, deixado explícito de propósito: XSS consegue ler o
// localStorage. Nesse laboratório isso é material de estudo, e não um
// acidente — o cenário de XSS vai conseguir roubar o token de uma vítima. Um
// cookie HttpOnly evitaria, ao custo de precisar de proteção CSRF.

const KEY = 'cp_session';

// Objeto de sessão vazio, no formato que o resto da interface consome.
const VAZIA = { token: null, user: null, expiresAt: null, since: null };

function lerBruto() {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    // localStorage indisponível (aba anônima, por exemplo): segue sem sessão
    // em vez de quebrar a aplicação.
    return null;
  }
}

/**
 * Sessão atual, ou null se não houver login válido.
 * O usuário volta no formato antigo ({ id, username, role, since }) para as
 * telas que já usavam isso.
 */
export function getSession() {
  const bruta = lerBruto();
  if (!bruta?.token || !bruta.user) return null;

  return {
    token: bruta.token,
    expiresAt: bruta.expiresAt ?? null,
    since: bruta.since ?? null,
    ...bruta.user,
  };
}

/** Só o token, para o cliente HTTP montar o header Authorization. */
export function getToken() {
  const bruta = lerBruto();
  return bruta?.token ?? null;
}

/**
 * Grava a sessão a partir da resposta de /api/auth/login ou /api/auth/register.
 * @param {{ token: string, expiresAt?: string, user: object }} resposta
 */
export function startSession(resposta) {
  const sessao = {
    token: resposta.token,
    expiresAt: resposta.expiresAt ?? null,
    since: new Date().toISOString(),
    user: resposta.user,
  };

  try {
    localStorage.setItem(KEY, JSON.stringify(sessao));
  } catch {
    // Sem storage: a sessão vale só para a aba atual.
  }

  return getSession();
}

/** Substitui só o token, sem perder o usuário. Usado após editar o perfil. */
export function updateToken(novoToken, novoExpiresAt) {
  const atual = lerBruto();
  if (!atual) return null;

  const sessao = { ...atual, token: novoToken ?? atual.token };
  if (novoExpiresAt) sessao.expiresAt = novoExpiresAt;

  try {
    localStorage.setItem(KEY, JSON.stringify(sessao));
  } catch {
    // Sem storage: segue sem persistir.
  }

  return getSession();
}

export function endSession() {
  try {
    localStorage.removeItem(KEY);
  } catch {
    // Nada a limpar.
  }
}

/** Verdadeiro quando o token já passou de expiresAt. */
export function isExpired() {
  const sessao = lerBruto();
  if (!sessao?.expiresAt) return false;
  return new Date(sessao.expiresAt) <= new Date();
}

export function initials(name = '') {
  const parts = name.replace(/[._-]+/g, ' ').trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? parts[0]?.[1] ?? '')).toUpperCase() || '?';
}