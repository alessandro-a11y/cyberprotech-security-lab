import { Route, Routes } from 'react-router-dom';
import Layout from './components/Layout.jsx';
import Dashboard from './pages/Dashboard.jsx';
import Lab from './pages/Lab.jsx';
import Login from './pages/Login.jsx';
import ToolkitResults from './pages/ToolkitResults.jsx';
import Users from './pages/Users.jsx';

// Fase 1: rotas base. Autenticação real e laboratórios
// vulneráveis controlados entram nas Fases 2 e 3.
export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Dashboard />} />
        <Route path="login" element={<Login />} />
        <Route path="users" element={<Users />} />
        <Route path="lab" element={<Lab />} />
        <Route path="toolkit" element={<ToolkitResults />} />
        <Route path="*" element={<Dashboard />} />
      </Route>
    </Routes>
  );
}
