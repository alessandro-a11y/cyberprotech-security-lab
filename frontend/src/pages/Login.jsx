import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import Icon from '../components/Icon.jsx';
import { sampleUsers } from '../data/sampleUsers.js';
import { api } from '../api/client.js';
import { getSession, startSession } from '../session.js';

// Autenticação real contra POST /api/auth/login. A API devolve o token JWT
// junto com o usuário; a sessão guarda os dois (ver session.js).
export default function Login() {
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState(null);
  const [enviando, setEnviando] = useState(false);

  if (getSession()) {
    return <Navigate to="/" replace />;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (!username.trim() || !password) {
      setError('Informe usuário e senha.');
      return;
    }

    setError(null);
    setEnviando(true);

    try {
      const resposta = await api.login(username.trim(), password);
      startSession(resposta);
      navigate('/', { replace: true });
    } catch (err) {
      // 429: o login tem rate limit de 10 tentativas por minuto por IP.
      // Sem tratar aqui, a tela mostraria só "Usuário ou senha inválidos" e
      // pareceria bug.
      if (err.status === 429) {
        setError('Muitas tentativas. Aguarde um minuto e tente de novo.');
      } else if (err.status === 401) {
        // A API devolve a mesma mensagem para usuário inexistente e senha
        // errada, de propósito: não revela quais contas existem.
        setError('Usuário ou senha inválidos.');
      } else {
        // O client.js já traduz falha de rede em mensagem utilizável.
        setError(err.message ?? 'Não foi possível falar com a API.');
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="auth">
      <section className="auth-visual">
        <div className="brand">
          <span className="brand-mark">
            <Icon name="shieldCheck" size={20} />
          </span>
          <span className="brand-name">
            <strong>CyberProtech</strong>
            <span>Security Lab</span>
          </span>
        </div>

        <div className="auth-pitch">
          <span className="eyebrow">// portal de gestão de usuários</span>
          <h2>
            Construir, atacar, <em>corrigir</em> e validar.
          </h2>
          <p>
            Aplicação propositalmente vulnerável para estudo de segurança web, rodando
            somente em laboratório controlado.
          </p>
          <div className="auth-steps">
            <span className="badge">01 desenvolver</span>
            <span className="badge danger">02 explorar</span>
            <span className="badge cyan">03 analisar</span>
            <span className="badge green">04 corrigir</span>
            <span className="badge">05 validar</span>
          </div>
        </div>

        <pre className="terminal" style={{ maxWidth: 440 }}>
          <span className="muted">$</span> toolkit --target http://backend:8080{'\n'}
          <span className="info">[info]</span> headers ............ 6 verificados{'\n'}
          <span className="warn">[warn]</span> cookie sem HttpOnly{'\n'}
          <span className="ok">[ ok ]</span> relatório gerado em reports/
        </pre>
      </section>

      <section className="auth-form-side">
        <div className="auth-card">
          <h1>Entrar no portal</h1>
          <p>Acesse com sua conta do laboratório.</p>

          <form className="auth-form" onSubmit={handleSubmit} noValidate>
            {error && (
              <div className="alert danger" role="alert">
                <Icon name="alert" size={16} /> {error}
              </div>
            )}

            <label className="field">
              Usuário
              <span className="input-wrap">
                <Icon name="user" size={16} />
                <input
                  className="input"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  autoComplete="username"
                  placeholder="ex.: aluno01"
                  autoFocus
                />
              </span>
            </label>

            <label className="field">
              Senha
              <span className="input-wrap">
                <Icon name="lock" size={16} />
                <input
                  className="input"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="current-password"
                  placeholder="••••••••"
                  style={{ paddingRight: '2.5rem' }}
                />
                <button
                  type="button"
                  className="btn btn-ghost btn-icon"
                  style={{ position: 'absolute', right: 4 }}
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}
                >
                  <Icon name={showPassword ? 'eyeOff' : 'eye'} size={16} />
                </button>
              </span>
            </label>

            <div className="auth-row">
              <label className="muted">
                <input type="checkbox" /> Manter conectado
              </label>
              <span className="muted">
                Senha dos exemplos: <code>CyberProtech@2026</code>
              </span>
            </div>

            <button type="submit" className="btn btn-primary btn-block" disabled={enviando}>
              {enviando ? 'Entrando…' : 'Entrar'} <Icon name="arrowRight" size={16} />
            </button>
          </form>

          <p className="auth-note">
            Protótipo com dados de exemplo: entre com uma das contas fictícias abaixo e qualquer
            senha. O login real chega na Fase 2.
            <br />
            {sampleUsers.map((u) => (
              <button
                key={u.id}
                type="button"
                className="badge"
                style={{ margin: '0.5rem 0.35rem 0 0', cursor: 'pointer' }}
                onClick={() => setUsername(u.username)}
              >
                {u.username}
              </button>
            ))}
          </p>
        </div>
      </section>
    </div>
  );
}
