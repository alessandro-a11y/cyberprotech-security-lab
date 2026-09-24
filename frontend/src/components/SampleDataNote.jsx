import { USE_SAMPLE_DATA } from '../api/client.js';
import Icon from './Icon.jsx';

// Aviso visível sempre que a tela estiver mostrando dados fictícios.
export default function SampleDataNote({ children }) {
  if (!USE_SAMPLE_DATA) return null;
  return (
    <div className="alert info" style={{ marginBottom: '1rem' }}>
      <Icon name="alert" size={16} />
      <span>
        <b>Dados de exemplo.</b>{' '}
        {children ?? 'Usuários fictícios, só para o protótipo. Serão trocados pelos dados da API.'}
      </span>
    </div>
  );
}
