import Icon from '../components/Icon.jsx';

// Resultados do Security Toolkit (Fase 4). Quando o toolkit exportar o
// relatório em JSON, esta tela passa a listar os achados por categoria.
const checks = [
  { name: 'Headers HTTP', desc: 'CSP, HSTS, X-Frame-Options, X-Content-Type-Options', icon: 'file' },
  { name: 'Cookies', desc: 'HttpOnly, Secure, SameSite', icon: 'key' },
  { name: 'Endpoints', desc: 'Rotas expostas e métodos aceitos', icon: 'server' },
  { name: 'Configuração', desc: 'CORS, debug, mensagens de erro', icon: 'settings' },
];

export default function ToolkitResults() {
  return (
    <>
      <div className="page-header">
        <div>
          <span className="eyebrow">// security toolkit</span>
          <h1>Resultados das análises</h1>
          <p>Relatórios gerados pelo toolkit em Python contra a aplicação do laboratório.</p>
        </div>
        <span className="badge">Python · CLI</span>
      </div>

      <div className="grid grid-2">
        <div className="card">
          <div className="card-header">
            <h2>Último relatório</h2>
          </div>
          <div className="empty">
            <span className="empty-icon">
              <Icon name="terminal" />
            </span>
            <h3>Nenhuma análise ainda</h3>
            <p>Rode o toolkit pelo Docker e o relatório aparece aqui quando a integração estiver pronta.</p>
          </div>
          <pre className="terminal">
            <span className="muted">$</span> docker compose --profile toolkit run --rm toolkit --target http://backend:8080
          </pre>
        </div>

        <div className="card">
          <div className="card-header">
            <div>
              <h2>O que é verificado</h2>
              <p>Módulos do toolkit</p>
            </div>
          </div>
          <ul className="list">
            {checks.map((c) => (
              <li key={c.name}>
                <span className="list-main">
                  <span className="stat-icon cyan">
                    <Icon name={c.icon} size={15} />
                  </span>
                  <span>
                    {c.name}
                    <small>{c.desc}</small>
                  </span>
                </span>
                <span className="badge">aguardando</span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </>
  );
}
