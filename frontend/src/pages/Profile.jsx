import { useEffect, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { api } from '../api/client.js';
import Icon from '../components/Icon.jsx';
import SampleDataNote from '../components/SampleDataNote.jsx';
import { getSession, initials } from '../session.js';

// O perfil é carregado pelo id da URL (/profile/:id → GET /api/users/{id}).
// É o ponto de entrada do cenário de IDOR (Fase 3): trocar o id mostra
// outro usuário. A correção (Fase 6) é a API checar o dono da sessão.
export default function Profile() {
  const { id } = useParams();
  const session = getSession();
  const [user, setUser] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setUser(null);
    setError(null);
    api
      .getUserById(id)
      .then((u) => !cancelled && setUser(u))
      .catch((err) => !cancelled && setError(err.message));
    return () => {
      cancelled = true;
    };
  }, [id]);

  if (!id) {
    return <Navigate to={`/profile/${session.id}`} replace />;
  }

  const isOwn = id === session.id;
  const since = new Date(session.since).toLocaleString('pt-BR');

  return (
    <>
      <div className="page-header">
        <div>
          <span className="eyebrow">// conta</span>
          <h1>{isOwn ? 'Meu perfil' : 'Perfil de usuário'}</h1>
          <p className="mono" style={{ fontSize: '0.8rem' }}>
            GET /api/users/{id}
          </p>
        </div>
        {!isOwn && (
          <Link to={`/profile/${session.id}`} className="btn">
            <Icon name="user" size={16} /> Voltar ao meu perfil
          </Link>
        )}
      </div>

      <SampleDataNote />

      {error && (
        <div className="card empty">
          <span className="empty-icon" style={{ color: 'var(--danger)' }}>
            <Icon name="user" />
          </span>
          <h3>Usuário não encontrado</h3>
          <p className="mono">{error}</p>
        </div>
      )}

      {!error && !user && (
        <div className="card stack">
          <div className="skeleton" style={{ width: '40%' }} />
          <div className="skeleton" style={{ width: '60%' }} />
        </div>
      )}

      {user && (
        <div className="grid grid-2">
          <div className="stack">
            <div className="card">
              <div className="profile-head">
                <span className="avatar lg">{initials(user.username)}</span>
                <div>
                  <h2>{user.username}</h2>
                  <p>{user.email}</p>
                  <div style={{ display: 'flex', gap: '0.4rem', marginTop: '0.5rem' }}>
                    <span className={`badge ${user.role === 'Admin' ? 'warn' : 'cyan'}`}>{user.role}</span>
                    {isOwn ? (
                      <span className="badge green">
                        <span className="dot" /> você
                      </span>
                    ) : (
                      <span className="badge danger">outro usuário</span>
                    )}
                  </div>
                </div>
              </div>
            </div>

            <div className="card">
              <div className="card-header">
                <h2>Bio</h2>
              </div>
              {/* Renderizada como texto (React escapa). O cenário de XSS da
                  Fase 3 troca por HTML sem escape, só no modo vulnerável. */}
              <p className={user.bio ? undefined : 'muted'}>{user.bio || 'Sem bio.'}</p>
            </div>

            <form className="card" onSubmit={(e) => e.preventDefault()}>
              <div className="card-header">
                <div>
                  <h2>Informações pessoais</h2>
                  <p>Edição habilitada na Fase 2</p>
                </div>
              </div>
              <div className="form-grid">
                <label className="field">
                  Usuário
                  <input className="input" value={user.username} disabled readOnly />
                </label>
                <label className="field">
                  E-mail
                  <input className="input" value={user.email} disabled readOnly />
                </label>
                <label className="field" style={{ gridColumn: '1 / -1' }}>
                  Bio
                  <input className="input" value={user.bio ?? ''} placeholder="Conte um pouco sobre você" disabled readOnly />
                </label>
              </div>
              <div style={{ marginTop: '1rem', display: 'flex', justifyContent: 'flex-end' }}>
                <button className="btn btn-primary" disabled>
                  Salvar alterações
                </button>
              </div>
            </form>
          </div>

          <div className="stack">
            <div className="card">
              <div className="card-header">
                <h2>Conta</h2>
                <Icon name="activity" size={16} className="muted" />
              </div>
              <dl className="kv">
                <dt>ID</dt>
                <dd title={user.id}>{user.id.slice(0, 13)}…</dd>
                <dt>Criada em</dt>
                <dd>{user.createdAt ? new Date(user.createdAt).toLocaleDateString('pt-BR') : '—'}</dd>
                <dt>Papel</dt>
                <dd>{user.role}</dd>
                {isOwn && (
                  <>
                    <dt>Sessão desde</dt>
                    <dd>{since}</dd>
                  </>
                )}
              </dl>
            </div>

            {isOwn && (
              <form className="card" onSubmit={(e) => e.preventDefault()}>
                <div className="card-header">
                  <h2>Alterar senha</h2>
                  <Icon name="key" size={16} className="muted" />
                </div>
                <div className="stack" style={{ gap: '0.75rem' }}>
                  <label className="field">
                    Senha atual
                    <input className="input" type="password" disabled />
                  </label>
                  <label className="field">
                    Nova senha
                    <input className="input" type="password" disabled />
                  </label>
                  <button className="btn" disabled>
                    Atualizar senha
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </>
  );
}
