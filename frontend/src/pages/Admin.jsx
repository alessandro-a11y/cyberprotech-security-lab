import { useEffect, useState } from 'react';
import { api, USE_SAMPLE_DATA } from '../api/client.js';
import { useRequest } from '../api/useRequest.js';
import Icon from '../components/Icon.jsx';
import SampleDataNote from '../components/SampleDataNote.jsx';
import { getSession } from '../session.js';

// Os toggles vêm de GET /api/lab/config. Os ids são os mesmos que o backend
// usa (vuln-mode, verbose-errors, rate-limit, sec-headers), então não há
// tradução: o switch da tela e o flag do servidor são o mesmo identificador.
//
// Gravar exige Admin e ambiente de desenvolvimento com Lab:Enabled=true
// (dupla trava no backend). Fora disso a API responde 409 e writable=false,
// então os switches aparecem desabilitados — que é o estado correto.
const CAMPOS_POR_ID = {
  'vuln-mode': 'vulnMode',
  'verbose-errors': 'verboseErrors',
  'rate-limit': 'rateLimit',
  'sec-headers': 'securityHeaders',
};

export default function Admin() {
  const session = getSession();
  const users = useRequest(api.getUsers);
  const stats = useRequest(api.getStats);
  const lab = useRequest(api.getLabConfig);

  const [toggles, setToggles] = useState([]);
  const [salvando, setSalvando] = useState(null);
  const [labErro, setLabErro] = useState(null);

  // Espelha o estado do servidor. Sem isso, a tela mostraria um switch ligado
  // enquanto a API está inteira no estado corrigido.
  useEffect(() => {
    if (lab.data?.toggles) setToggles(lab.data.toggles);
  }, [lab.data]);

  const list = users.data ?? [];

  // As contagens vêm do banco quando a API está de pé. A lista inteira continua
  // sendo usada como reserva (e nos dados de exemplo).
  const total = stats.data?.totalUsers ?? list.length;
  const admins = stats.data?.admins ?? list.filter((u) => u.role?.toLowerCase() === 'admin').length;
  const pct = stats.data?.adminPercentage ?? (list.length ? Math.round((admins / list.length) * 100) : 0);
  const podeGravar = Boolean(lab.data?.writable);

  async function flip(toggle) {
    // Otimista: a UI responde na hora e reverte se a API recusar.
    const novos = toggles.map((t) => (t.id === toggle.id ? { ...t, on: !t.on } : t));
    setToggles(novos);
    setSalvando(toggle.id);
    setLabErro(null);

    try {
      const resposta = await api.updateLabConfig({ [CAMPOS_POR_ID[toggle.id]]: !toggle.on });
      if (resposta?.toggles) setToggles(resposta.toggles);
    } catch (err) {
      setToggles(toggles); // reverte
      setLabErro(
        err.status === 409
          ? (err.message ?? 'Fora de desenvolvimento ou laboratório desabilitado.')
          : (err.message ?? 'Não foi possível alterar o controle.'),
      );
    } finally {
      setSalvando(null);
    }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <span className="eyebrow">// área restrita</span>
          <h1>Administração</h1>
          <p>Configurações do portal e controle do laboratório.</p>
        </div>
      </div>

      <SampleDataNote>As contagens vêm de usuários fictícios até a API responder.</SampleDataNote>

      {session.role !== 'Admin' && (
        <div className="alert warn" style={{ marginBottom: '1rem' }}>
          <Icon name="alert" size={16} />
          <span>
            Você está como <b>User</b> e mesmo assim abriu esta página. O servidor bloqueia: cada
            chamada a <code>/api/admin/*</code> devolve <b>403</b>, e os controles do laboratório ficam
            desabilitados. Mostrar os dois lados é a demonstração do cenário de <b>Broken Access
            Control</b> — o bloqueio real está na API, não nesta tela.
          </span>
        </div>
      )}

      <div className="grid grid-3">
        <div className="card">
          <div className="card-header">
            <h2>Contas</h2>
            <Icon name="users" size={16} className="muted" />
          </div>
          <div className="stat-value">
            {stats.loading ? '…' : stats.error && users.loading ? '—' : total}
          </div>
          <div className="stat-foot">
            {stats.error ? 'GET /api/admin/stats indisponível' : 'total de usuários'}
          </div>
        </div>
        <div className="card">
          <div className="card-header">
            <h2>Administradores</h2>
            <Icon name="key" size={16} className="muted" />
          </div>
          <div className="stat-value">
            {stats.loading ? '…' : stats.error && users.loading ? '—' : admins}
          </div>
          <div className="bar" style={{ marginTop: '0.9rem' }}>
            <span style={{ width: `${pct}%`, background: 'var(--warn)' }} />
          </div>
          <div className="stat-foot">{pct}% das contas com privilégio</div>
        </div>
        <div className="card">
          <div className="card-header">
            <h2>Banco de dados</h2>
            <Icon name="database" size={16} className="muted" />
          </div>
          <dl className="kv">
            <dt>Engine</dt>
            <dd>PostgreSQL</dd>
            <dt>ORM</dt>
            <dd>EF Core</dd>
            <dt>Status</dt>
            <dd style={{ color: USE_SAMPLE_DATA ? 'var(--cyan)' : users.error ? 'var(--danger)' : 'var(--accent)' }}>
              {USE_SAMPLE_DATA ? 'dados de exemplo' : users.loading ? '…' : users.error ? 'sem resposta' : 'conectado'}
            </dd>
          </dl>
        </div>
      </div>

      <div className="card" style={{ marginTop: '1rem' }}>
        <div className="card-header">
          <div>
            <h2>Controles do laboratório</h2>
            <p>
              GET/PUT /api/lab/config
              {!podeGravar && ' — leitura apenas: exige Admin, ambiente Development e Lab:Enabled=true'}
            </p>
          </div>
          <span className="badge warn">LAB</span>
        </div>

        {lab.loading && <p className="muted">Carregando controles…</p>}
        {lab.error && <div className="alert danger">Não foi possível ler os controles: {lab.error}</div>}
        {labErro && <div className="alert danger">{labErro}</div>}

        {lab.data && (
          <ul className="list">
            {toggles.map((t) => (
              <li key={t.id}>
                <span className="list-main">
                  <span>
                    {t.label}
                    <small>{t.hint}</small>
                  </span>
                </span>
                <button
                  type="button"
                  role="switch"
                  aria-checked={t.on}
                  aria-label={t.label}
                  className="switch"
                  // `disabled` e não só visual: sem permissão, o clique não
                  // deve nem chegar à API.
                  disabled={!podeGravar || salvando === t.id}
                  onClick={() => flip(t)}
                />
              </li>
            ))}
          </ul>
        )}

        <p className="muted" style={{ marginTop: '1rem', marginBottom: 0, fontSize: '0.85rem' }}>
          O estado é em memória no servidor: reiniciar a API devolve tudo ao padrão seguro. Ligar o
          modo vulnerável registra um aviso alto no log — não deixe ligado fora da demonstração.
        </p>
      </div>
    </>
  );
}
