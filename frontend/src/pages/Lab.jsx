// Fase 1: placeholder do laboratório. Os cenários vulneráveis
// controlados (SQLi, XSS, IDOR etc.) serão implementados na Fase 3,
// sempre restritos ao ambiente Docker deste projeto.
const planned = [
  'SQL Injection',
  'Cross-Site Scripting (XSS)',
  'IDOR / Broken Access Control',
  'Falhas de autenticação',
  'Security Misconfiguration',
  'Exposição de informações',
];

export default function Lab() {
  return (
    <section>
      <h1>Laboratório</h1>
      <p>
        Cenários vulneráveis propositalmente criados para estudo, executados
        somente neste ambiente controlado. Nenhum cenário implementado ainda (Fase 3).
      </p>
      <ul className="list">
        {planned.map((item) => (
          <li key={item}>
            {item} — <em>planejado</em>
          </li>
        ))}
      </ul>
    </section>
  );
}
