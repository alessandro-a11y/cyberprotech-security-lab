import { useState } from 'react';

// Fase 1: tela placeholder. O fluxo de login real (Fase 2)
// e os cenários de falhas de autenticação (Fase 3) ainda serão implementados.
export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  function handleSubmit(event) {
    event.preventDefault();
    alert('Login ainda não implementado (Fase 2). Ambiente de laboratório controlado.');
  }

  return (
    <section>
      <h1>Login</h1>
      <p>Autenticação será implementada na Fase 2.</p>
      <form className="card form" onSubmit={handleSubmit}>
        <label>
          Usuário
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            placeholder="ex.: aluno01"
          />
        </label>
        <label>
          Senha
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            placeholder="••••••••"
          />
        </label>
        <button type="submit">Entrar</button>
      </form>
    </section>
  );
}
