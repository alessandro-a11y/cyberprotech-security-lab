import { useState } from 'react';
import { api, USE_SAMPLE_DATA } from '../api/client.js';
import { useRequest } from '../api/useRequest.js';
import Icon from '../components/Icon.jsx';
import SampleDataNote from '../components/SampleDataNote.jsx';
import { getSession } from '../session.js';

// Chaves do laboratório. Na Fase 3 cada uma liga/desliga a versão
// vulnerável de um cenário via API; por enquanto são só visuais.
const initialToggles = [
  { id: 'vuln-mode', label: 'Modo vulnerável', hint: 'Ativa os cenários inseguros do laboratório', on: true },
  { id: 'verbose-errors', label: 'Erros detalhados', hint: 'Expõe stack trace nas respostas da API', on: false },
  { id: 'rate-limit', label: 'Limite de tentativas de login', hint: 'Bloqueia força bruta', on: false },
  { id: 'sec-headers', label: 'Headers de segurança', hint: 'CSP, HSTS, X-Frame-Options', on: false },
];

export default function Admin() {
  const session = getSession();
  const users = useRequest(api.getUsers);
  const [toggles, setToggles] = useState(initialToggles);

  const list = users.data ?? [];
  const admins = list.filter((u) => u.role?.toLowerCase() === 'admin').length;
  const pct = list.length ? Math.round((admins / list.length) * 100) : 0;

  function flip(id) {
    setToggles((ts) => ts.map((t) => (t.id === id ? { ...t, on: !t.on } : t)));
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

      <SampleDataNote>As contagens de contas usam usuários fictícios, só para o protótipo.</SampleDataNote>

      {session.role !== 'Admin' && (
        <div className="alert warn" style={{ marginBottom: '1rem' }}>
          <Icon name="alert" size={16} />
          <span>
            Você está como <b>User</b> e mesmo assim abriu esta página. Isso é de propósito: o bloqueio
            por papel ainda não existe, e esse é o cenário de <b>Broken Access Control</b> do laboratório.
          </span>
        </div>
      )}

      <div className="grid grid-3">
        <div className="card">
          <div className="card-header">
            <h2>Contas</h2>
            <Icon name="users" size={16} className="muted" />
          </div>
          <div className="stat-value">{users.loading ? '…' : users.error ? '—' : list.length}</div>
          <div className="stat-foot">total de usuários</div>
        </div>
        <div className="card">
          <div className="card-header">
            <h2>Administradores</h2>
            <Icon name="key" size={16} className="muted" />
          </div>
          <div className="stat-value">{users.loading ? '…' : users.error ? '—' : admins}</div>
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
            <p>Protótipo visual. A integração com a API entra na Fase 3.</p>
          </div>
          <span className="badge warn">LAB</span>
        </div>
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
                onClick={() => flip(t.id)}
              />
            </li>
          ))}
        </ul>
      </div>
    </>
  );
}
