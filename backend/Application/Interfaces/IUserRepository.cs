using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Contrato de acesso a dados de usuários.
/// Implementação Entity Framework em Infrastructure (Fase 1: leitura e busca).
/// </summary>
public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista usuários cujo usuário ou e-mail contenha <paramref name="search"/>.
    /// Termo vazio ou nulo devolve todos os usuários.
    /// </summary>
    /// <remarks>
    /// Na Fase 3 este método é o ponto de entrada do cenário de SQL Injection:
    /// a implementação com LINQ parametrizado será trocada por uma consulta
    /// concatenada de propósito, para fins didáticos.
    /// </remarks>
    Task<IReadOnlyList<User>> SearchAsync(string? search, CancellationToken cancellationToken = default);
}
