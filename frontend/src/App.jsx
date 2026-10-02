import { Navigate, Route, Routes } from 'react-router-dom';
import Layout from './components/Layout.jsx';
import Admin from './pages/Admin.jsx';
import Dashboard from './pages/Dashboard.jsx';
import Lab from './pages/Lab.jsx';
import Login from './pages/Login.jsx';
import Profile from './pages/Profile.jsx';
import ToolkitResults from './pages/ToolkitResults.jsx';
import Users from './pages/Users.jsx';
import ErrorBoundary from './components/ErrorBoundary.jsx';

// Login fica fora do Layout (tela cheia). As demais rotas exigem a
// sessão de protótipo; o login real entra na Fase 2.
export default function App() {
  return (
    <ErrorBoundary><Routes>
      <Route path="login" element={<Login />} />
      <Route element={<Layout />}>
        <Route index element={<Dashboard />} />
        <Route path="users" element={<Users />} />
        <Route path="profile" element={<Profile />} />
        <Route path="profile/:id" element={<Profile />} />
        <Route path="admin" element={<Admin />} />
        <Route path="lab" element={<Lab />} />
        <Route path="toolkit" element={<ToolkitResults />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes></ErrorBoundary>
  );
}
