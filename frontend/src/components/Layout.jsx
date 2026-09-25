import { useState } from 'react';
import { Navigate, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { endSession, getSession, initials } from '../session.js';
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
  const session = getSession();

  if (!session) {
    return <Navigate to="/login" replace />;
  }

  function logout() {
    endSession();
    navigate('/login', { replace: true });
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
