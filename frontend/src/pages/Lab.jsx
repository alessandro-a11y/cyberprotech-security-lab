import Icon from '../components/Icon.jsx';
import { severityBadge, statusBadge, vulnerabilities } from '../data/vulnerabilities.js';

// Cenários vulneráveis controlados (Fase 3), sempre restritos ao
// ambiente Docker deste projeto. Status editado em data/vulnerabilities.js.
export default function Lab() {
  return (
    <>
      <div className="page-header">
        <div>
          <span className="eyebrow">// laboratório</span>
          <h1>Cenários de vulnerabilidade</h1>
          <p>
            Falhas criadas de propósito para estudo, baseadas no OWASP Top 10. Cada cenário é
            explorado, analisado pelo toolkit, corrigido e validado de novo.
          </p>
        </div>
      </div>

      <div className="alert warn" style={{ marginBottom: '1.25rem' }}>
        <Icon name="alert" size={16} />
        <span>Uso exclusivamente educacional, só neste ambiente. Não aplicar em sistemas de terceiros.</span>
      </div>

      <div className="grid grid-3">
        {vulnerabilities.map((v) => (
          <article key={v.id} className="card vuln-card">
            <div className="card-header" style={{ marginBottom: 0 }}>
              <span className={`stat-icon ${severityBadge[v.severity].tone}`}>
                <Icon name={v.icon} size={16} />
              </span>
              <span style={{ display: 'flex', gap: '0.35rem' }}>
                <span className={`badge ${statusBadge[v.status].tone}`}>{statusBadge[v.status].label}</span>
                <span className={`badge ${severityBadge[v.severity].tone}`}>{severityBadge[v.severity].label}</span>
              </span>
            </div>
            <h3>{v.name}</h3>
            <p>{v.description}</p>
            <div className="vuln-meta">
              <span className="owasp">{v.owasp}</span>
            </div>
          </article>
        ))}
      </div>
    </>
  );
}
