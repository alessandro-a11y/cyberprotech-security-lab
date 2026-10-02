// Todo cenário já está implementado no servidor. Quando o controle está
// desligado, ele continua disponível para a demonstração, porém protegido.
const FLAG_BY_SCENARIO = {
  sqli: 'vuln-mode', idor: 'vuln-mode', xss: 'vuln-mode',
  auth: 'rate-limit', misconfig: 'sec-headers', exposure: 'verbose-errors',
};

export function statusDoCenario(id, toggles) {
  if (!toggles) return { status: 'desconhecido', detail: 'estado não consultado' };
  const flag = FLAG_BY_SCENARIO[id];
  const ligado = toggles.find((toggle) => toggle.id === flag)?.on;
  return ligado
    ? { status: 'vulneravel', detail: `controle ${flag} ligado` }
    : { status: 'corrigido', detail: `implementado · proteção ${flag} ativa` };
}
