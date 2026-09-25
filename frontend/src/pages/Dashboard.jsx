import { Link } from 'react-router-dom';
import { api, API_BASE_URL } from '../api/client.js';
import { useRequest } from '../api/useRequest.js';
import Icon from '../components/Icon.jsx';
import SampleDataNote from '../components/SampleDataNote.jsx';
import { severityBadge, statusBadge, vulnerabilities } from '../data/vulnerabilities.js';
import { getSession } from '../session.js';

const cycle = ['Desenvolver', 'Explorar', 'Analisar', 'Corrigir', 'Validar'];
// Etapa atual do projeto (0 = Desenvolver). Avançar conforme as fases.
const currentStep = 0;

function Stat({ label, value, foot, icon, tone }) {
  return (
    <div className="card stat">
      <div className="stat-top">
        {label}
        <span className={`stat-icon ${tone}`}>
          <Icon name={icon} size={16} />
        </span>
      </div>
      <div className="stat-value">{value}</div>
      <div className="stat-foot">{foot}</div>
    </div>
  );
}

export default function Dashboard() {
  const session = getSession();
  const health = useRequest(api.getHealth);
  const users = useRequest(api.getUsers);

  const userList = users.data ?? [];
  const admins = userList.filter((u) => u.role?.toLowerCase() === 'admin').length;
  const fixed = vulnerabilities.filter((v) => v.status === 'corrigido').length;
  const online = !health.loading && !health.error;

  return (
    <>
      <div className="page-header">
        <div>
          <span className="eyebrow">// visão geral</span>
          <h1>Olá, {session.username}</h1>
          <p>Estado do laboratório, da API e dos cenários de vulnerabilidade.</p>
        </div>
        <button className="btn" onClick={() => { health.reload(); users.reload(); }}>
          <Icon name="refresh" size={16} /> Atualizar
        </button>
      </div>

      <SampleDataNote>
        O total de usuários vem de contas fictícias. Status da API e cenários são reais.
      </SampleDataNote>

      <div className="grid grid-stats">
        <Stat
          label="Usuários"
          value={users.loading ? '…' : users.error ? '—' : userList.length}
          foot={users.error ? 'API indisponível' : `${admins} administrador(es)`}
          icon="users"
          tone="cyan"
        />
        <Stat
          label="API"
          value={health.loading ? '…' : online ? 'ON' : 'OFF'}
          foot={API_BASE_URL.replace(/^https?:\/\//, '')}
          icon="server"
          tone={online ? 'green' : 'danger'}
        />
        <Stat
          label="Vulnerabilidades"
          value={vulnerabilities.length}
          foot="cenários mapeados (OWASP)"
          icon="bug"
          tone="danger"
        />
        <Stat
          label="Corrigidas"
          value={`${fixed}/${vulnerabilities.length}`}
          foot="validadas pelo toolkit"
          icon="shieldCheck"
          tone="warn"
        />
      </div>

      <div className="card" style={{ marginTop: '1rem' }}>
        <div className="card-header">
          <h2>Ciclo do projeto</h2>
          <span className="badge green">etapa {currentStep + 1} de {cycle.length}</span>
        </div>
        <div className="grid" style={{ gridTemplateColumns: `repeat(${cycle.length}, 1fr)`, gap: '0.5rem' }}>
          {cycle.map((step, i) => (
            <div key={step}>
              <div className="bar">
                <span style={{ width: i <= currentStep ? '100%' : '0%' }} />
              </div>
              <div
                className="mono"
                style={{
                  marginTop: '0.5rem',
                  fontSize: '0.75rem',
                  color: i <= currentStep ? 'var(--accent)' : 'var(--text-dim)',
                }}
              >
                {String(i + 1).padStart(2, '0')} {step}
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="grid grid-2" style={{ marginTop: '1rem' }}>
        <div className="card">
          <div className="card-header">
            <div>
              <h2>Cenários do laboratório</h2>
              <p>Status de cada vulnerabilidade controlada</p>
            </div>
            <Link to="/lab" className="btn btn-ghost">
              Ver todos <Icon name="arrowRight" size={14} />
            </Link>
          </div>
          <ul className="list">
            {vulnerabilities.map((v) => (
              <li key={v.id}>
                <span className="list-main">
                  <span className="stat-icon">
                    <Icon name={v.icon} size={15} />
                  </span>
                  <span>
                    {v.name}
                    <small>{v.owasp}</small>
                  </span>
                </span>
                <span style={{ display: 'flex', gap: '0.35rem' }}>
                  <span className={`badge ${severityBadge[v.severity].tone}`}>
                    {severityBadge[v.severity].label}
                  </span>
                  <span className={`badge ${statusBadge[v.status].tone}`}>{statusBadge[v.status].label}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>

        <div className="card">
          <div className="card-header">
            <div>
              <h2>Status da API</h2>
              <p>GET /api/health</p>
            </div>
            <span className={`badge ${health.loading ? '' : online ? 'green' : 'danger'}`}>
              <span className={`dot${online ? ' pulse' : ''}`} />
              {health.loading ? 'consultando' : online ? 'online' : 'offline'}
            </span>
          </div>
          <pre className="terminal">
            <span className="muted">$</span> curl {API_BASE_URL}/api/health{'\n'}
            {health.loading && <span className="muted">aguardando resposta…</span>}
            {health.error && <span className="err">[erro] {health.error}</span>}
            {health.data && <span className="ok">{JSON.stringify(health.data, null, 2)}</span>}
          </pre>
        </div>
      </div>
    </>
  );
}
