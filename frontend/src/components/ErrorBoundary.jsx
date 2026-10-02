import { Component } from 'react';

export default class ErrorBoundary extends Component {
  constructor(props) { super(props); this.state = { error: null }; }
  static getDerivedStateFromError(error) { return { error }; }
  render() {
    if (!this.state.error) return this.props.children;
    return <main className="content"><div className="card empty"><h1>Não foi possível abrir esta tela</h1><p>Atualize a página ou volte ao início. O erro foi isolado para manter o portal utilizável.</p><button className="btn" onClick={() => this.setState({ error: null })}>Tentar novamente</button></div></main>;
  }
}
