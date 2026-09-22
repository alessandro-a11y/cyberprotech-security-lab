// Fase 1: placeholder da tela de resultados do Security Toolkit.
// A exibição de relatórios gerados pelo toolkit (Fase 4) será ligada aqui.
export default function ToolkitResults() {
  return (
    <section>
      <h1>Resultados do Security Toolkit</h1>
      <p>
        O toolkit Python gerará relatórios (headers, cookies, endpoints,
        configurações) que serão exibidos nesta tela a partir da Fase 4.
      </p>
      <div className="card">
        <p>Nenhum relatório disponível ainda.</p>
      </div>
    </section>
  );
}
