using Domain.Entities;

namespace Application.DTOs;

/// <summary>
/// Representação pública de um usuário (sem hash de senha).
/// </summary>
/// <param name="Id">Identificador do usuário.</param>
/// <param name="Username">Nome de login.</param>
/// <param name="Email">E-mail do usuário.</param>
/// <param name="Role">Papel do usuário ("Admin" ou "User").</param>
/// <param name="Bio">Texto de apresentação exibido no perfil.</param>
/// <param name="CreatedAt">Data de criação do cadastro (UTC).</param>
public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    string? Bio = null,
    DateTime CreatedAt = default)
{
    /// <summary>
    /// Projeção da entidade para o DTO. Ponto único de mapeamento para evitar
    /// que o PasswordHash vaze para fora da camada de aplicação.
    /// </summary>
    public static UserDto FromEntity(User user) =>
        new(user.Id, user.Username, user.Email, user.Role, user.Bio, user.CreatedAt);
}
