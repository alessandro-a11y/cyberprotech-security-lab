import { useEffect, useState } from 'react';
import { api } from '../api/client.js';

export default function Users() {
  const [users, setUsers] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .getUsers()
      .then(setUsers)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  return (
    <section>
      <h1>Usuários</h1>
      <p>Lista consumida da API (GET /api/users).</p>
      {loading && <p>Carregando…</p>}
      {error && <p className="error">Falha ao carregar usuários: {error}</p>}
      {!loading && !error && (
        <table className="table">
          <thead>
            <tr>
              <th>Usuário</th>
              <th>E-mail</th>
              <th>Papel</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id ?? u.username}>
                <td>{u.username}</td>
                <td>{u.email}</td>
                <td>{u.role}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
