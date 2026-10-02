import { useEffect, useState } from 'react';
import { Navigate, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { endSession, getSession, initials, isExpired } from '../session.js';
import { api } from '../api/client.js';
import Icon from './Icon.jsx';

const sections = [
  {
    label: 'Portal',
    links: [
      { to: '/', label: 'Dashboard', icon: 'dashboard' },
      { to: '/users', label: 'Usuários', icon: 'users' },
      { to: '/profile', label: 'Meu perfil', icon: 'user' },
      { to: '/admin', label: 'Administração', icon: 'settings' },
    ],
  },
  {
    label: 'Segurança',
    links: [
      { to: '/lab', label: 'Laboratório', icon: 'flask' },
      { to: '/toolkit', label: 'Security Toolkit', icon: 'terminal' },
    ],
  },
];

const allLinks = sections.flatMap((s) => s.links);

function pageTitle(pathname) {
  if (pathname === '/') return 'Dashboard';
  return allLinks.find((l) => l.to !== '/' && pathname.startsWith(l.to))?.label ?? 'Página';
}

export default function Layout() {
  const [menuOpen, setMenuOpen] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();

  // A sessão vive no localStorage, mas só a API sabe se o token ainda vale.
  // Por isso o restore é assíncrono: primeiro conferimos, depois renderizamos.
  // Sem isso, recarregar a página deixaria a interface montada com um token
  // expirado e a primeira chamada já cairia em 401.
  const [session, setSession] = useState(() => getSession());
  const [conferindo, setConferindo] = useState(() => Boolean(getSession()));

  useEffect(() => {
    const atual = getSession();

    if (!atual) {
      setSession(null);
      setConferindo(false);
      return;
    }

    // Token expirado por data: nem vale a chamada.
    if (isExpired()) {
      endSession();
      setSession(null);
      setConferindo(false);
      return;
    }

    let cancelado = false;
    setConferindo(true);

    api
      .me()
      .then((user) => {
        if (cancelado) return;
        // Confia na resposta da API, não no storage: se o usuário foi
        // removido ou teve o papel alterado, a tela reflete o estado real.
        setSession((s) => ({ ...s, ...user }));
      })
      .catch(() => {
        if (cancelado) return;
        // 401 (token inválido ou expirado) ou API fora do ar: nos dois casos
        // a sessão local não serve mais.
        endSession();
        setSession(null);
      })
      .finally(() => !cancelado && setConferindo(false));

    return () => {
      cancelado = true;
    };
  }, []);

  if (!session && !conferindo) {
    return <Navigate to="/login" replace />;
  }

  function logout() {
    endSession();
    setSession(null);
    navigate('/login', { replace: true });
  }

  if (!session) {
    // Enquanto confere, não mostra o esqueleto nem manda para o login: o
    // usuário que acabou de entrar veria um piscar de tela.
    return (
      <div className="app">
        <main className="content">
          <div className="card">Conferindo sessão…</div>
        </main>
      </div>
    );
  }

  return (
    <div className="app">
      <aside className={`sidebar${menuOpen ? ' open' : ''}`} onClick={() => setMenuOpen(false)}>
        <NavLink to="/" className="brand">
          <span className="brand-mark">
            <Icon name="shieldCheck" size={20} />
          </span>
          <span className="brand-name">
            <strong>CyberProtech</strong>
            <span>Security Lab</span>
          </span>
        </NavLink>

        <nav className="nav">
          {sections.map((section) => (
            <div key={section.label}>
              <div className="nav-label">{section.label}</div>
              {section.links.map((link) => (
                <NavLink key={link.to} to={link.to} end={link.to === '/'}>
                  <Icon name={link.icon} />
                  {link.label}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <div className="sidebar-footer">
          <span className="avatar">{initials(session.username)}</span>
          <div className="sidebar-user">
            <strong>{session.username}</strong>
            <span>{session.role}</span>
          </div>
          <button className="btn btn-ghost btn-icon" onClick={logout} title="Sair" aria-label="Sair">
            <Icon name="logout" />
          </button>
        </div>
      </aside>

      <div className="main">
        <header className="topbar">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <button
              className="btn btn-ghost btn-icon menu-toggle"
              onClick={() => setMenuOpen(true)}
              aria-label="Abrir menu"
            >
              <Icon name="menu" />
            </button>
            <span className="breadcrumb">
              cyberprotech / <b>{pageTitle(location.pathname)}</b>
            </span>
          </div>
          <div className="topbar-right">
            <span className="badge warn">
              <span className="dot" /> LAB · ambiente controlado
            </span>
          </div>
        </header>

        <main className="content">
          <Outlet />
        </main>

        <footer className="footer">
          Uso exclusivamente educacional em ambiente controlado. Não utilizar contra sistemas externos.
        </footer>
      </div>
    </div>
  );
}
