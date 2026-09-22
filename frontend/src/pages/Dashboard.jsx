import { useEffect, useState } from 'react';
import { api } from '../api/client.js';

export default function Dashboard() {
  const [health, setHealth] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    api
      .getHealth()
      .then(setHealth)
      .catch((err) => setError(err.message));
  }, []);

  return (
    <section>
      <h1>Dashboard</h1>
      <p>
        Visão geral do laboratório. Em fases futuras, exibirá o status da API,
        do banco de dados e os últimos resultados do Security Toolkit.
      </p>
      <div className="card">
        <h2>Status da API</h2>
        {error && <p className="error">API indisponível: {error}</p>}
        {health ? (
          <pre>{JSON.stringify(health, null, 2)}</pre>
        ) : (
          !error && <p>Consultando /api/health…</p>
        )}
      </div>
    </section>
  );
}
