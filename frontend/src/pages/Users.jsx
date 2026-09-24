import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client.js';
import Icon from '../components/Icon.jsx';
import SampleDataNote from '../components/SampleDataNote.jsx';
import { initials } from '../session.js';

const filters = [
  { id: 'all', label: 'Todos' },
  { id: 'admin', label: 'Admins' },
  { id: 'user', label: 'Usuários' },
];

export default function Users() {
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState('all');
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [attempt, setAttempt] = useState(0);

  // A busca é feita pela API (GET /api/users?search=), não no navegador:
  // é o ponto de entrada do cenário de SQL Injection (Fase 3).
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    const timer = setTimeout(() => {
      api
        .getUsers(query)
        .then((users) => !cancelled && setData(users))
        .catch((err) => !cancelled && setError(err.message))
        .finally(() => !cancelled && setLoading(false));
    }, 300);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query, attempt]);

  const users = (data ?? []).filter((u) => filter === 'all' || u.role?.toLowerCase() === filter);

  return (
    <>
      <div className="page-header">
        <div>
          <span className="eyebrow">// gerenciamento</span>
          <h1>Usuários</h1>
          <p>Contas cadastradas no portal. Busca via GET /api/users?search=.</p>
        </div>
        <button className="btn btn-primary" disabled title="Cadastro entra na Fase 2">
          <Icon name="plus" size={16} /> Novo usuário
        </button>
      </div>

      <SampleDataNote />

      <div className="toolbar">
        <span className="input-wrap">
          <Icon name="search" size={16} />
          <input
            className="input"
            placeholder="Buscar por usuário ou e-mail"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </span>
        <div className="segmented" role="tablist">
          {filters.map((f) => (
            <button
              key={f.id}
              role="tab"
              aria-selected={filter === f.id}
              className={filter === f.id ? 'active' : undefined}
              onClick={() => setFilter(f.id)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {error ? (
        <div className="card empty">
          <span className="empty-icon" style={{ color: 'var(--danger)' }}>
            <Icon name="server" />
          </span>
          <h3>Não foi possível carregar os usuários</h3>
          <p className="mono">{error}</p>
          <button className="btn" onClick={() => setAttempt((n) => n + 1)}>
            <Icon name="refresh" size={16} /> Tentar de novo
          </button>
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Usuário</th>
                <th>E-mail</th>
                <th>Papel</th>
                <th>ID</th>
              </tr>
            </thead>
            <tbody>
              {loading &&
                [0, 1, 2].map((i) => (
                  <tr key={i}>
                    {[0, 1, 2, 3].map((j) => (
                      <td key={j}>
                        <div className="skeleton" style={{ width: `${50 + ((i + j) % 3) * 15}%` }} />
                      </td>
                    ))}
                  </tr>
                ))}
              {!loading &&
                users.map((u) => (
                  <tr key={u.id ?? u.username}>
                    <td>
                      <Link to={`/profile/${u.id}`} className="cell-user" style={{ color: 'inherit', textDecoration: 'none' }}>
                        <span className="avatar">{initials(u.username)}</span>
                        <span>{u.username}</span>
                      </Link>
                    </td>
                    <td className="muted">{u.email}</td>
                    <td>
                      <span className={`badge ${u.role?.toLowerCase() === 'admin' ? 'warn' : 'cyan'}`}>
                        {u.role}
                      </span>
                    </td>
                    <td className="mono" style={{ fontSize: '0.72rem', color: 'var(--text-dim)' }}>
                      {u.id?.slice(0, 8)}…
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
          {!loading && users.length === 0 && (
            <div className="empty">
              <span className="empty-icon">
                <Icon name="users" />
              </span>
              <h3>{query || filter !== 'all' ? 'Nenhum usuário encontrado' : 'Nenhum usuário cadastrado'}</h3>
              <p>
                {query || filter !== 'all'
                  ? 'Ajuste a busca ou o filtro.'
                  : 'A tabela de usuários no PostgreSQL ainda está vazia.'}
              </p>
            </div>
          )}
        </div>
      )}
    </>
  );
}
