using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

/// <summary>
/// Dados de cadastro de um novo usuário.
/// </summary>
/// <remarks>
/// Não existe campo de papel aqui de propósito: o auto-cadastro sempre cria
/// <c>User</c>. Aceitar um <c>role</c> vindo do cliente seria escalada de
/// privilégio — o campo errado que a Fase 3 pode reintroduzir como laboratório.
/// </remarks>
public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Informe o nome de usuário.")]
    [StringLength(64, MinimumLength = 3, ErrorMessage = "O usuário deve ter entre 3 e 64 caracteres.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Use apenas letras, números, ponto, hífen e sublinhado.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(256, ErrorMessage = "O e-mail deve ter no máximo 256 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "A senha deve ter entre 8 e 128 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [StringLength(512, ErrorMessage = "A bio deve ter no máximo 512 caracteres.")]
    public string? Bio { get; set; }
}

/// <summary>
/// Credenciais de login.
/// </summary>
public sealed class LoginRequest
{
    [Required(ErrorMessage = "Informe o usuário.")]
    [StringLength(64, ErrorMessage = "Usuário inválido.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(128, ErrorMessage = "Senha inválida.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Resposta de um login (ou cadastro) bem-sucedido.
/// </summary>
public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

/// <summary>
/// Resposta da atualização do próprio perfil: os dados atualizados e um token
/// novo, já com o e-mail atualizado na claim.
/// </summary>
public sealed record ProfileUpdateResponse(UserDto User, string Token, DateTime ExpiresAt);
