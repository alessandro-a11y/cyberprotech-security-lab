namespace Application.DTOs;

/// <summary>
/// Representação pública de um usuário (sem hash de senha).
/// </summary>
public sealed record UserDto(Guid Id, string Username, string Email, string Role);
