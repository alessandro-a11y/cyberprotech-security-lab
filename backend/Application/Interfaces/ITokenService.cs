using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Emissão de tokens de sessão. A assinatura/validação criptográfica fica em
/// Infrastructure; aqui mora apenas o contrato.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Gera um token para o usuário, contendo o papel como claim de autorização.
    /// </summary>
    /// <returns>Token e instante de expiração em UTC.</returns>
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}
