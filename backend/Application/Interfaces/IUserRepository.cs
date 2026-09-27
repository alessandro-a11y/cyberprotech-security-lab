using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Contrato de acesso a dados de usuários.
/// </summary>
public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual a <see cref="GetByIdAsync"/>, mas devolve a entidade rastreada para
    /// que alterações e remoções possam ser gravadas com
    /// <see cref="SaveChangesAsync"/>. Não use para simples leitura.
    /// </summary>
    Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Busca com filtro de papel e paginação (<c>skip</c>/<c>take</c>).
    /// <paramref name="role"/> nulo ou vazio não filtra.
    /// </summary>
    Task<IReadOnlyList<User>> SearchAsync(
        string? search,
        string? role,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca por usuário, ignorando maiúsculas/minúsculas. É o método usado
    /// pelo login — por isso compara sem traduzir para o banco.
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o usuário ou o e-mail já estão em uso.
    /// </summary>
    Task<bool> ExistsByUsernameOrEmailAsync(
        string username,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verdadeiro quando o e-mail já pertence a <paramref name="excludeUserId"/>
    /// <em>ou a outro</em> usuário. O perfil usa isso para permitir reenviar o
    /// próprio e-mail sem dar conflito.
    /// </summary>
    Task<bool> EmailInUseByOtherAsync(
        string email,
        Guid excludeUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quantas contas têm exatamente o papel informado.
    /// </summary>
    Task<int> CountByRoleAsync(string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Total de contas cadastradas.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inclui um novo usuário. Lança se violar a restrição de unicidade.
    /// </summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste as alterações de uma entidade já rastreada.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um usuário.
    /// </summary>
    Task DeleteAsync(User user, CancellationToken cancellationToken = default);
}
