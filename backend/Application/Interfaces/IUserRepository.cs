using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Contrato de acesso a dados de usuários.
/// Implementação Entity Framework em Infrastructure (Fase 1: somente leitura).
/// </summary>
public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
