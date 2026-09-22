import { NavLink, Outlet } from 'react-router-dom';

const links = [
  { to: '/', label: 'Dashboard' },
  { to: '/login', label: 'Login' },
  { to: '/users', label: 'Usuários' },
  { to: '/lab', label: 'Laboratório' },
  { to: '/toolkit', label: 'Security Toolkit' },
];

export default function Layout() {
  return (
    <div className="app">
      <header className="topbar">
        <span className="brand">CyberProtech Security Lab</span>
        <nav>
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end={link.to === '/'}
              className={({ isActive }) => (isActive ? 'active' : undefined)}
            >
              {link.label}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="content">
        <Outlet />
      </main>
      <footer className="footer">
        Uso exclusivamente educacional em ambiente controlado. Não utilizar contra sistemas externos.
      </footer>
    </div>
  );
}
