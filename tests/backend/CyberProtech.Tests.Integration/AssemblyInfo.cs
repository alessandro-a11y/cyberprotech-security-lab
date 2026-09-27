using Xunit;

// Os testes de integração compartilham um único PostgreSQL e, em alguns casos,
// o mesmo usuário (o aluno01 tem a bio reescrita pelo cenário de XSS). Rodar
// classes em paralelo faria uma classe ler o que outra está escrevendo, e o
// resultado viraria flaky sem causa aparente.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
