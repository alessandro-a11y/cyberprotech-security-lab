import { useEffect, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { api } from '../api/client.js';
import Icon from '../components/Icon.jsx';
import SampleDataNote from '../components/SampleDataNote.jsx';
import { getSession, initials, updateToken } from '../session.js';

// O perfil é carregado pelo id da URL (/profile/:id → GET /api/users/{id}).
// É o ponto de entrada do cenário de IDOR (Fase 3): trocar o id mostra
// outro usuário. A correção (Fase 6) é a API checar o dono da sessão.
export default function Profile() {
  const { id } = useParams();
  const session = getSession();
  const [user, setUser] = useState(null);
  const [error, setError] = useState(null);

  // Edição do próprio perfil: só email e bio. Username é somente-leitura
  // (a API ignora) e role/password não passam por este endpoint.
  const [email, setEmail] = useState('');
  const [bio, setBio] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [perfilMsg, setPerfilMsg] = useState(null);

  // Troca de senha: exige a senha atual, senão um token vazado bastaria para
  // tomar a conta inteira.
  const [senhaAtual, setSenhaAtual] = useState('');
  const [novaSenha, setNovaSenha] = useState('');
  const [trocando, setTrocando] = useState(false);
  const [senhaMsg, setSenhaMsg] = useState(null);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setUser(null);
    setError(null);
    api
      .getUserById(id)
      .then((u) => {
        if (cancelled) return;
        setUser(u);
        setEmail(u.email ?? '');
        setBio(u.bio ?? '');
      })
      .catch((err) => !cancelled && setError(err.message));
    return () => {
      cancelled = true;
    };
  }, [id]);

  async function salvarPerfil(event) {
    event.preventDefault();
    setSalvando(true);
    setPerfilMsg(null);

    try {
      // A API devolve um token novo, já com o email atualizado na claim.
      const resposta = await api.updateProfile({ email, bio });
      if (resposta?.user) setUser((u) => ({ ...u, ...resposta.user }));
      if (resposta?.token) updateToken(resposta.token, resposta.expiresAt);
      setBio(resposta?.user?.bio ?? '');
      setPerfilMsg({ tipo: 'ok', texto: 'Perfil atualizado.' });
    } catch (err) {
      setPerfilMsg({
        tipo: 'erro',
        texto:
          err.status === 409
            ? 'E-mail já cadastrado.'
            : (err.message ?? 'Não foi possível salvar.'),
      });
    } finally {
      setSalvando(false);
    }
  }

  async function trocarSenha(event) {
    event.preventDefault();
    setTrocando(true);
    setSenhaMsg(null);

    try {
      await api.changePassword(senhaAtual, novaSenha);
      setSenhaAtual('');
      setNovaSenha('');
      setSenhaMsg({ tipo: 'ok', texto: 'Senha alterada.' });
    } catch (err) {
      setSenhaMsg({
        tipo: 'erro',
        texto:
          err.status === 401
            ? 'Senha atual inválida.'
            : (err.message ?? 'Não foi possível alterar a senha.'),
      });
    } finally {
      setTrocando(false);
    }
  }

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

            <form className="card" onSubmit={salvarPerfil}>
              <div className="card-header">
                <div>
                  <h2>Informações pessoais</h2>
                  <p>PUT /api/auth/me — email e bio</p>
                </div>
              </div>
              <div className="form-grid">
                <label className="field">
                  Usuário
                  <input className="input" value={user.username} disabled readOnly />
                </label>
                <label className="field">
                  E-mail
                  <input
                    className="input"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    disabled={!isOwn}
                  />
                </label>
                <label className="field" style={{ gridColumn: '1 / -1' }}>
                  Bio
                  <input
                    className="input"
                    value={bio}
                    onChange={(e) => setBio(e.target.value)}
                    placeholder="Conte um pouco sobre você"
                    maxLength={512}
                    disabled={!isOwn}
                  />
                </label>
              </div>
              {perfilMsg && (
                <div className={`alert ${perfilMsg.tipo === 'ok' ? 'success' : 'danger'}`} style={{ marginTop: '1rem' }}>
                  <Icon name={perfilMsg.tipo === 'ok' ? 'check' : 'alert'} size={16} />
                  <span>{perfilMsg.texto}</span>
                </div>
              )}
              <div style={{ marginTop: '1rem', display: 'flex', justifyContent: 'flex-end' }}>
                <button className="btn btn-primary" type="submit" disabled={!isOwn || salvando}>
                  {salvando ? 'Salvando…' : 'Salvar alterações'}
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
              <form className="card" onSubmit={trocarSenha}>
                <div className="card-header">
                  <div>
                    <h2>Alterar senha</h2>
                    <p>POST /api/auth/change-password</p>
                  </div>
                  <Icon name="key" size={16} className="muted" />
                </div>
                <div className="stack" style={{ gap: '0.75rem' }}>
                  <label className="field">
                    Senha atual
                    <input
                      className="input"
                      type="password"
                      value={senhaAtual}
                      onChange={(e) => setSenhaAtual(e.target.value)}
                      autoComplete="current-password"
                    />
                  </label>
                  <label className="field">
                    Nova senha
                    <input
                      className="input"
                      type="password"
                      value={novaSenha}
                      onChange={(e) => setNovaSenha(e.target.value)}
                      minLength={8}
                      autoComplete="new-password"
                      placeholder="mínimo de 8 caracteres"
                    />
                  </label>
                  {senhaMsg && (
                    <div className={`alert ${senhaMsg.tipo === 'ok' ? 'success' : 'danger'}`}>
                      <Icon name={senhaMsg.tipo === 'ok' ? 'check' : 'alert'} size={16} />
                      <span>{senhaMsg.texto}</span>
                    </div>
                  )}
                  <button className="btn" type="submit" disabled={trocando || !senhaAtual || !novaSenha}>
                    {trocando ? 'Alterando…' : 'Atualizar senha'}
                  </button>
                  <p className="muted" style={{ margin: 0, fontSize: '0.85rem' }}>
                    A senha nova passa a valer no próximo login. Tokens já emitidos continuam válidos
                    até expirar — limitação conhecida do JWT sem estado.
                  </p>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </>
  );
}
